using System.Collections;
using UnityEngine;

public class CoroutineResult<Res> : CoroutineResult<Res, string> { }

public class AgentSystem : MonoBehaviour
{
    [SerializeField] LLMBackend llm;
    [SerializeField] AnimationLibrary animationLibrary;
    [SerializeField] UserTestLogger logger;
    public bool sendAllContext = false;
    [field: SerializeField] public ContextLibrary contextLibrary { get; private set; }
    [field: SerializeField] public ChatManager chatManager { get; private set; }

    public AnimationLibrary AnimationLibrary => animationLibrary;
    public bool IsBusy { get; private set; }
    public ActionsResponse LastActions { get; private set; }

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

    IEnumerator RunTurn(string userPrompt)
    {
        CoroutineResult<ActionsResponse> actionRes = new();
        if (sendAllContext)
        {
            yield return StartCoroutine(GetActionsJson(userPrompt, ContextQuery.GetFullContext(), actionRes));
        }
        else
        {
            CoroutineResult<ContextQuery> queryRes = new();
            yield return StartCoroutine(GetContextJson(userPrompt, queryRes));
            if(queryRes.Status == ContextStatus.Failure) yield break;

            yield return StartCoroutine(GetActionsJson(userPrompt, queryRes.Response, actionRes));
            if (actionRes.Status == ContextStatus.Failure) yield break;

        }
        contextLibrary.AddMessageToHistory(userPrompt);
    }

    public IEnumerator GetContextJson(string userPrompt, CoroutineResult<ContextQuery> res = null)
    {
        res ??= new();
        if (IsBusy)
        {
            string err = "Still processing a previous message, wait until it's done";
            Debug.LogWarning(err);
            res.SetError(err);
            yield break;
        }
        SetBusiness(true);
        Debug.Log($"Sending to backend Round 1: {userPrompt}");

        yield return StartCoroutine(llm.GetContext(userPrompt, res));

        if (res.Status == ContextStatus.Success)
        {
            string backendResponse = JsonUtility.ToJson(res.Response, true);
            Debug.Log($"Backend context: {backendResponse}");
            Debug.Log($"completion tokens: {res.Response.completion_tokens}\nprompt tokens: {res.Response.prompt_tokens}");
            logger?.LogTurn(userPrompt, backendResponse);
        }
        else
        {
            Debug.LogError($"Error getting context: {res.Error}");
            AddChat($"System: Error getting context: {res.Error}");
            logger?.LogTurn(userPrompt, res.Error);
        }
        SetBusiness(false);
    }

    public void SetBusiness(bool busy)
    {
        IsBusy = busy;
        if (chatManager != null) chatManager.SetActiveSending(!busy);
    }

    public IEnumerator GetActionsJson(string userPrompt, ContextQuery context, CoroutineResult<ActionsResponse> res = null)
    {
        res ??= new();
        if (IsBusy)
        {
            string err = "Still processing a previous message, wait until it's done";
            Debug.LogWarning(err);
            res.SetError(err);
            yield break;
        }
        SetBusiness(true);
        string world = contextLibrary.GetContext(context, animationLibrary.animations);
        Debug.Log($"Sending to backend Round 2:\n{world}\n{userPrompt}");

        yield return StartCoroutine(llm.GetActions(userPrompt, world, res));

        if (res.Status == ContextStatus.Success)
        {
            Debug.Log($"completion tokens: {res.Response.completion_tokens}\nprompt tokens: {res.Response.prompt_tokens}");
            string backendJson = JsonUtility.ToJson(res.Response, true);
            Debug.Log($"Backend actions: {backendJson}");
            logger?.LogTurn(userPrompt, backendJson);
            LastActions = res.Response;
            ActionsSuccess(res.Response);
        }
        else
        {
            Debug.LogError($"Error getting context: {res.Error}");
            AddChat("System: " + res.Error);
            logger?.LogTurn(userPrompt, res.Error);
        }
        SetBusiness(false);

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
}
