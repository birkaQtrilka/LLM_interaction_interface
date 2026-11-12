using System;
using System.Collections;
using System.Collections.Generic;
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
    public AnimationLibrary AnimationLibrary => animationLibrary;
    public bool IsBusy { get; private set; }
    public ActionsResponse LastActions { get; private set; }

    // On only in scenes that play the scenario, so other scenes make no judge calls
    [SerializeField] bool checkScenario = false;
    // How long nothing may run before the scenario check is sent. Between Round 1 and Round 2 of a turn IsBusy is briefly false
    [SerializeField] float settleSeconds = 0.5f;
    // Evidence for the judge: actions finished since the current checkpoint began
    readonly List<string> finishedSinceAdvance = new();
    bool checkPending;

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
            bool settled = !IsBusy && animationLibrary.animations.TrueForAll(a => a.isFinished);
            quiet = settled ? quiet + Time.deltaTime : 0f;
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
        // The list is evidence for one state, so the next state starts with an empty list
        if (r.advanced) finishedSinceAdvance.Clear();
    }

    private void OnUserMessage(string txt)
    {
        StartCoroutine(RunSystem(txt));
    }

    public IEnumerator RunSystem(string userPrompt)
    {
        if(IsBusy)
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

        CoroutineResult<ActionsResponse> actionRes = new();
        if (sendAllContext)
        {
            yield return StartCoroutine(GetActionsJson(userPrompt, ContextQuery.GetFullContext(), actionRes, addressee, speaker));
            if (actionRes.Status == ContextStatus.Failure) yield break;
        }
        else
        {
            CoroutineResult<ContextQuery> queryRes = new();
            yield return StartCoroutine(GetContextJson(userPrompt, queryRes));
            if(queryRes.Status == ContextStatus.Failure) yield break;

            yield return StartCoroutine(GetActionsJson(userPrompt, queryRes.Response, actionRes, addressee, speaker));
            if (actionRes.Status == ContextStatus.Failure) yield break;

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

    public IEnumerator GetActionsJson(string userPrompt, ContextQuery context, CoroutineResult<ActionsResponse> res = null, string addressee = null, string speaker = null)
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
                if (speaker == null)
                    contextLibrary.AddMessageToHistory("user", string.IsNullOrEmpty(addressee) ? "everyone" : addressee, userPrompt);
                ActionsSuccess(res.Response);
            }
            else
            {
                Debug.LogError($"Error getting context: {res.Error}");
                AddChat("System: " + res.Error);
            }
        }
    }

    public void SetBusiness(bool busy)
    {
        IsBusy = busy;
        if (chatManager != null) chatManager.SetActiveSending(!busy);
        if(loadingAnimator != null)
        {
            if(busy) loadingAnimator.Play();
            else loadingAnimator.Stop();
        }
    }

    void ActionsSuccess(ActionsResponse reply)
    {
        if (reply.actions == null)
        {
            return;
        }
        bool wasErr = false;
        foreach (var action in reply.actions)
        {
            string error = animationLibrary.PlayAnimation(this, action);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.Log(error);
                AddChat("System: " + error);
                wasErr = true;
                break;
            }
        }
        if (wasErr)
        {
            animationLibrary.animations.Clear();
        }
    }

    void AddChat(string message) 
    {
        if (chatManager == null) return;
        chatManager.AddChat(message);
    }

    public NPC GetAgent(string name)
    {
        return contextLibrary.agents.Find(a=>a.name == name);
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
