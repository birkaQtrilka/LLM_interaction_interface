using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CoroutineResult<Res> : CoroutineResult<Res, string> { }

public class AgentSystem : MonoBehaviour
{
    [SerializeField] LLMBackend llm;
    [SerializeField] AnimationLibrary animationLibrary;
    public bool sendAllContext = false;
    [field: SerializeField] public ContextLibrary contextLibrary { get; private set; }
    [field: SerializeField] public ChatManager chatManager { get; private set; }
    [SerializeField] UISpriteAnimator loadingAnimator;

    [Header("Action error feedback")]
    [Tooltip("How many times in a row errors may be sent back to the backend after one player/NPC message")]
    [SerializeField] int maxFeedbackRounds = 2;
    [Tooltip("Errors arriving within this window are batched into one feedback message")]
    [SerializeField] float errorBatchDelay = 0.3f;

    public AnimationLibrary AnimationLibrary => animationLibrary;
    public bool IsBusy { get; private set; }
    public ActionsResponse LastActions { get; private set; }

    // Errors waiting to be sent to the backend
    readonly List<string> pendingErrors = new();
    float errorFlushAt;
    int feedbackRounds;
    bool turnInProgress;

    // What the last real turn looked like, so feedback can replay it with the same context
    bool hasLastTurn;
    string lastRequest;
    ContextQuery lastQuery;
    string lastAddressee;
    string lastSpeaker;
    int planCounter;

    public void Init(LLMBackend llm, AnimationLibrary anim, ContextLibrary ctx)
    {
        this.llm = llm;
        this.animationLibrary = anim;
        this.contextLibrary = ctx;
    }

    private void Awake()
    {
        if (chatManager == null) return;
        chatManager.OnTextSent.AddListener(OnUserMessage);
    }

    private void Update()
    {
        // Flush queued errors once the system is idle
        if (pendingErrors.Count == 0 || !hasLastTurn) return;
        if (turnInProgress || IsBusy || Time.time < errorFlushAt) return;
        StartCoroutine(RunFeedbackTurn());
    }

    private void OnUserMessage(string txt)
    {
        StartCoroutine(RunSystem(txt));
    }

    /// <summary>
    /// Call this from anywhere (including actions that fail later, after they were queued)
    /// to tell the backend something went wrong. Errors are batched and sent automatically.
    /// Pass the action that failed so the backend knows which part of its plan caused the error.
    /// </summary>
    public void ReportActionError(string error, NPC agent = null, ActionData action = null)
    {
        int planId = 0;
        if (action != null)
            planId = animationLibrary.GetAnimation(a => a.data.id == action.id)?.planId ?? 0;
        ReportError(error, agent, action, planId);
    }

    void ReportError(string error, NPC agent, ActionData action, int planId)
    {
        if (string.IsNullOrEmpty(error)) return;

        string msg = agent != null ? $"[{agent.name}] {error}" : error;
        if (action != null) msg += $" (failed action: {JsonUtility.ToJson(action)})";

        // Stop the rest of the plan; the failed action itself is already described above
        var cancelled = animationLibrary.CancelPlan(planId)
            .Where(c => action == null || c.anim.data.id != action.id)
            .Select(c => $"{c.anim.data.name} (id {c.anim.data.id}, {(c.wasRunning ? "was running" : "had not started")})")
            .ToList();
        if (cancelled.Count > 0)
            msg += $" The rest of the plan was cancelled: {string.Join(", ", cancelled)}.";

        Debug.LogWarning($"Action error: {msg}");

        if (pendingErrors.Contains(msg)) return;
        if (pendingErrors.Count == 0) errorFlushAt = Time.time + errorBatchDelay;
        pendingErrors.Add(msg);
    }

    public IEnumerator RunSystem(string userPrompt)
    {
        if (IsBusy)
        {
            Debug.LogWarning("Still processing a previous message, wait until it's done");
            yield break;
        }

        LastActions = null;
        yield return RunTurn(userPrompt);
    }

    // spokenTo is the nurse who just heard a line, null means the player and look-cast picks the addressee
    IEnumerator RunTurn(string userPrompt, string spokenTo = null, string speaker = null)
    {
        string addressee = spokenTo;
        if (spokenTo == null)
        {
            LookCast look = Camera.main != null ? Camera.main.GetComponent<LookCast>() : null;
            addressee = look != null ? look.AddresseeName : null;
        }

        turnInProgress = true;
        try
        {
            ContextQuery query;
            if (sendAllContext)
            {
                query = ContextQuery.GetFullContext();
            }
            else
            {
                CoroutineResult<ContextQuery> queryRes = new();
                yield return StartCoroutine(GetContextJson(userPrompt, queryRes));
                if (queryRes.Status == ContextStatus.Failure) yield break;
                query = queryRes.Response;
            }

            // New real turn: remember it for feedback and reset the retry budget
            hasLastTurn = true;
            lastRequest = userPrompt;
            lastQuery = query;
            lastAddressee = addressee;
            lastSpeaker = speaker;
            feedbackRounds = 0;
            pendingErrors.Clear();

            CoroutineResult<ActionsResponse> actionRes = new();
            yield return StartCoroutine(GetActionsJson(userPrompt, query, actionRes, addressee, speaker));
        }
        finally
        {
            turnInProgress = false;
        }
    }

    // Sends queued errors back to the backend and plays the corrected plan
    IEnumerator RunFeedbackTurn()
    {
        turnInProgress = true;
        try
        {
            List<string> errors = new(pendingErrors);
            pendingErrors.Clear();

            if (feedbackRounds >= maxFeedbackRounds)
            {
                Debug.LogWarning($"Giving up on action errors after {feedbackRounds} feedback rounds:\n- {string.Join("\n- ", errors)}");
                yield break;
            }
            feedbackRounds++;

            string feedback =
                $"Original request: {lastRequest}\n" +
                "System feedback: your previous plan had problems:\n" +
                $"- {string.Join("\n- ", errors)}\n" +
                "Provide a new plan that avoids these problems.";

            CoroutineResult<ActionsResponse> res = new();
            yield return StartCoroutine(GetActionsJson(feedback, lastQuery, res, lastAddressee, lastSpeaker, isFeedback: true));
        }
        finally
        {
            turnInProgress = false;
        }
    }

    IEnumerator RunBusy(IEnumerator work, Action<string> onBusy = null)
    {
        if (IsBusy)
        {
            string err = "Still processing a previous message, wait until it's done";
            Debug.LogWarning(err);
            onBusy?.Invoke(err);
            yield break;
        }

        SetBusiness(true);
        try
        {
            yield return StartCoroutine(work);
        }
        finally
        {
            SetBusiness(false);
        }
    }

    // Talk queues NPC-to-NPC lines here so the addressee gets a turn with speaker set
    // Not wrapped in RunBusy: GetContextJson and GetActionsJson inside RunTurn take the lock themselves, like a player turn
    public IEnumerator RunHeardTurn(string message, string spokenTo, string speaker)
    {
        yield return RunTurn(message, spokenTo, speaker);
    }

    public IEnumerator GetContextJson(string userPrompt, CoroutineResult<ContextQuery> res = null)
    {
        res ??= new();
        yield return RunBusy(Body(), res.SetError);

        IEnumerator Body()
        {
            Debug.Log($"Sending to backend Round 1: {userPrompt}");
            yield return StartCoroutine(llm.GetContext(userPrompt, res));

            if (res.Status == ContextStatus.Success)
            {
                string backendResponse = JsonUtility.ToJson(res.Response, true);
                Debug.Log($"Backend context: {backendResponse}");
                Debug.Log($"completion tokens: {res.Response.completion_tokens}\nprompt tokens: {res.Response.prompt_tokens}");
            }
            else
            {
                Debug.LogError($"Error getting context: {res.Error}");
                AddChat($"System: Error getting context: {res.Error}");
            }
        }
    }

    public IEnumerator GetActionsJson(string userPrompt, ContextQuery context, CoroutineResult<ActionsResponse> res = null,
        string addressee = null, string speaker = null, bool isFeedback = false)
    {
        res ??= new();
        yield return RunBusy(Body(), res.SetError);

        IEnumerator Body()
        {
            string world = contextLibrary.GetContext(context, animationLibrary.animations);
            Debug.Log($"Sending to backend Round 2:\n{world}\n{userPrompt}");
            yield return StartCoroutine(llm.GetActions(userPrompt, world, res, addressee, speaker));

            if (res.Status == ContextStatus.Success)
            {
                Debug.Log($"completion tokens: {res.Response.completion_tokens}\nprompt tokens: {res.Response.prompt_tokens}");
                string backendJson = JsonUtility.ToJson(res.Response, true);
                Debug.Log($"Backend actions: {backendJson}");
                LastActions = res.Response;
                // No speaker means the player said it. Stored before ActionsSuccess so it lands before the NPC replies (Talk stores those)
                // Feedback turns are system text, not speech, so they stay out of the history
                if (speaker == null && !isFeedback)
                    contextLibrary.AddMessageToHistory("user", string.IsNullOrEmpty(addressee) ? "everyone" : addressee, userPrompt);

                // The new plan supersedes errors reported by the old one
                pendingErrors.Clear();
                ActionsSuccess(res.Response);
            }
            else
            {
                Debug.LogError($"Error getting actions: {res.Error}");
                AddChat("System: " + res.Error);
            }
        }
    }

    public void SetBusiness(bool busy)
    {
        IsBusy = busy;
        if (chatManager != null) chatManager.SetActiveSending(!busy);
        if (loadingAnimator != null)
        {
            if (busy) loadingAnimator.Play();
            else loadingAnimator.Stop();
        }
    }

    void ActionsSuccess(ActionsResponse reply)
    {
        if (reply.actions == null) return;

        int planId = ++planCounter;
        foreach (var action in reply.actions)
        {
            string error = animationLibrary.PlayAnimation(this, action, planId);
            if (string.IsNullOrEmpty(error)) continue;

            // The failed action may never have been queued, so pass the plan id explicitly
            ReportError(error, null, action, planId);
            break;
        }
    }

    void AddChat(string message)
    {
        if (chatManager == null) return;
        chatManager.AddChat(message);
    }

    public NPC GetAgent(string name)
    {
        return contextLibrary.agents.Find(a => a.name == name);
    }

    public ContextItem GetObject(string name)
    {
        return contextLibrary.environment.Find(a => a.GetName() == name);
    }

    public Transform GetSpot(string name)
    {
        return contextLibrary.spots.Find(a => a.GetName() == name)?.transform;
    }

    public AnimationInstance GetAnim(int id)
    {
        return animationLibrary.animations.Find(a => a.data.id == id);
    }


}