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
    ILLMBackend injectedBackend;
    ILLMBackend Backend => injectedBackend ?? (llm != null ? llm : null);

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

    // No turn running and no action queued or playing
    public bool IsSettled => !IsBusy && animationLibrary != null && animationLibrary.animations.TrueForAll(a => a.isFinished);
    // Checkpoint id from the last scenario check. A new session starts at 1 on the backend too
    public int CurrentCheckpoint { get; private set; } = 1;


    // On only in scenes that play the scenario, so other scenes make no judge calls
    [SerializeField] bool checkScenario = false;
    // How long nothing may run before the scenario check is sent. Between Round 1 and Round 2 of a turn IsBusy is briefly false
    [SerializeField] float settleSeconds = 0.5f;
    // Evidence for the judge: actions finished since the current checkpoint began
    readonly List<string> finishedSinceAdvance = new();
    bool checkPending;

    public void SetBackend(ILLMBackend backend) => injectedBackend = backend;

    // Lets tests shorten the batching delay without reflection
    public void Configure(int maxFeedbackRounds, float errorBatchDelay)
    {
        this.maxFeedbackRounds = maxFeedbackRounds;
        this.errorBatchDelay = errorBatchDelay;
    }

    public void Init(ILLMBackend llm, AnimationLibrary anim, ContextLibrary ctx)
    {
        this.injectedBackend = llm;
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

    // Start, not Awake, because tests set animationLibrary through Init after Awake
    private void Start()
    {
        if (!checkScenario) return;
        // Test scenes play actions without a backend, so there is nothing to check against
        if (animationLibrary == null || llm == null)
        {
            Debug.LogWarning("AgentSystem has no AnimationLibrary or LLMBackend, so scenario checkpoints are never checked");
            return;
        }
        animationLibrary.AnimationEnded += OnAnimationEnded;
    }

    private void OnDestroy()
    {
        if (animationLibrary != null) animationLibrary.AnimationEnded -= OnAnimationEnded;
    }

    void OnAnimationEnded(AnimationInstance anim)
    {
        // Spoken lines are already in the chat history the judge gets, so only physical actions are listed
        if (anim.data.name != "talk")
            finishedSinceAdvance.Add($"{anim.data.agent} {anim.data.name}({string.Join(", ", anim.data.parameters ?? new string[0])})");
        if (!checkPending)
            StartCoroutine(CheckScenarioWhenSettled());
    }

    // Waits until no action is queued or playing and no turn is running, so the judge never sees a half-played plan
    IEnumerator CheckScenarioWhenSettled()
    {
        checkPending = true;
        float quiet = 0f;
        while (quiet < settleSeconds)
        {
            quiet = IsSettled ? quiet + Time.deltaTime : 0f;
            yield return null;
        }
        checkPending = false;
        // Busy during the call, so the player's next turn is built with the new checkpoint
        yield return RunBusy(CheckScenario());
    }

    IEnumerator CheckScenario()
    {
        string world = contextLibrary.GetContext(ContextQuery.GetFullContext(), animationLibrary.animations);
        CoroutineResult<ScenarioCheckResponse> res = new();
        yield return StartCoroutine(llm.CheckScenario(world, finishedSinceAdvance.ToArray(), res));
        if (res.Status != ContextStatus.Success)
        {
            AddChat("System: Scenario check failed: " + res.Error);
            yield break;
        }

        ScenarioCheckResponse r = res.Response;
        Debug.Log($"Scenario check: advanced={r.advanced}, now checkpoint {r.checkpoint} ({r.name}). {r.reason}");
        CurrentCheckpoint = r.checkpoint;
        // The list is evidence for one state, so the next state starts with an empty list
        if (r.advanced) finishedSinceAdvance.Clear();
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
            .Where(c => action == null || !ReferenceEquals(c.anim.data, action))
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
            var backend = Backend;
            if (backend == null) { res.SetError("No LLM backend configured"); Debug.LogError(res.Error); yield break; }
            Debug.Log($"Sending to backend Round 1: {userPrompt}");
            yield return StartCoroutine(backend.GetContext(userPrompt, res));

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
            var backend = Backend;
            if (backend == null) { res.SetError("No LLM backend configured"); Debug.LogError(res.Error); yield break; }
            string world = contextLibrary.GetContext(context, animationLibrary.animations);
            Debug.Log($"Sending to backend Round 2:\n{world}\n{userPrompt}");
            yield return StartCoroutine(backend.GetActions(userPrompt, world, res, addressee, speaker));

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

    public NPC GetAgent(string name) => contextLibrary.agents.Find(a => a != null && a.name == name);
    public ContextItem GetObject(string name) => contextLibrary.environment.Find(a => a.transform != null && a.GetName() == name);

    public Transform GetSpot(string name)
    {
        return contextLibrary.spots.Find(a => a.GetName() == name)?.transform;
    }

    public AnimationInstance GetAnim(int id)
    {
        return animationLibrary.animations.Find(a => a.data.id == id);
    }


}