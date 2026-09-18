using System;
using System.Collections;
using UnityEngine;
using static UnityEngine.Audio.ProcessorInstance;

[Serializable]
public class ActionData
{
    public int id;
    public string name;
    public string[] parameters;
    public int[] runAfter;
    public float delayBefore;
}

public enum ContextStatus { Success, Failure }

public class CoroutineResult<Res, Err>
{
    public ContextStatus Status;
    public Res Response;
    public Err Error;
    public bool IsDone;

    public void SetResult(Res r)
    {
        Status = ContextStatus.Success;
        Response = r;
        IsDone = true;
    } 

    public void SetError(Err e)
    {
        Status = ContextStatus.Failure;
        Error = e;
        IsDone = true;
    }
}

public class CoroutineResult<Res> : CoroutineResult<Res, string> { }

public class AgentSystem : MonoBehaviour
{
    [SerializeField] LLMBackend llm;
    [SerializeField] AnimationLibrary animationLibrary;
    public bool sendAllContext = false;
    [field: SerializeField] public ContextLibrary contextLibrary { get; private set; }
    [field: SerializeField] public ChatManager chatManager { get; private set; }
    public string lastUserPrompt;

    private void Awake()
    {
        chatManager.OnTextSent.AddListener(OnUserMessage);
    }

    private void OnUserMessage(string txt)
    {
        StartCoroutine(RunSystem(txt));
    }

    public IEnumerator RunSystem(string userPrompt)
    {
        lastUserPrompt = userPrompt;
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
        Debug.Log($"Sending to backend Round 1: {userPrompt}");

        yield return StartCoroutine(llm.GetContext(userPrompt, res));

        if (res.Status == ContextStatus.Success)
        {
            Debug.Log($"Backend context: {JsonUtility.ToJson(res.Response, true)}");
        }
        else
        {
            Debug.LogError($"Error getting context: {res.Error}");
            chatManager.AddChat($"Error getting context: {res.Error}");
        }
    }

    public IEnumerator GetActionsJson(string userPrompt, ContextQuery context, CoroutineResult<ActionsResponse> res = null)
    {
        res ??= new();
        string world = contextLibrary.GetContext(context, animationLibrary.animations);
        Debug.Log($"Sending to backend Round 2:\n{world}\n{userPrompt}");

        yield return StartCoroutine(llm.GetActions(userPrompt, world, res));

        if (res.Status == ContextStatus.Success)
        {
            Debug.Log($"Backend actions: {JsonUtility.ToJson(res.Response, true)}");
            ActionsSuccess(res.Response);
        }
        else
        {
            Debug.LogError($"Error getting context: {res.Error}");
            chatManager.AddChat(res.Error);
        }
    }

    void ActionsSuccess(ActionsResponse reply)
    {
        if (reply.actions == null)
        {
            return;
        }

        foreach (var action in reply.actions)
        {
            string error = animationLibrary.PlayAnimation(this, action);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.Log(error);
                chatManager.AddChat(error);
            }
        }
    }
}
