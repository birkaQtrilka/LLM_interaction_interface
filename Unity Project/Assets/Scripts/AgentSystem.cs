using System;
using UnityEngine;

[Serializable]
public class ActionData
{
    public int id;
    public string name;
    public string[] parameters;
    public int[] runAfter;
    public float delayBefore;
}

[Serializable]
public class ActionResponse
{
    public ActionData[] actions;
}

public class AgentSystem : MonoBehaviour
{
    [SerializeField] LLMBackend llm;
    [SerializeField] AnimationLibrary animationLibrary;
    public bool sendAllContext = false;
    [field: SerializeField] public ContextLibrary contextLibrary { get; private set; }
    [field: SerializeField] public ChatManager chatManager { get; private set; }

    private void Awake()
    {
        chatManager.OnTextSent.AddListener(OnUserMessage);
    }

    private void OnUserMessage(string txt)
    {
        if (sendAllContext)
        {
            GetActionsJson(txt, ContextResponse.GetFullContext());
        }
        else
        {
            GetContextJson(txt);
        }
        contextLibrary.AddMessageToHistory(txt);
    }

    private void GetContextJson(string userPrompt)
    {
        Debug.Log($"Sending to backend Round 1: {userPrompt}");

        llm.RequestContext(userPrompt,
            onSuccess: (response) =>
            {
                Debug.Log($"Backend context: {JsonUtility.ToJson(response, true)}");
                GetActionsJson(userPrompt, response);
            },
            onError: chatManager.AddChat
        );
    }

    void GetActionsJson(string userPrompt, ContextResponse context)
    {
        string world = contextLibrary.GetContext(context, animationLibrary.animations);
        Debug.Log($"Sending to backend Round 2:\n{world}\n{userPrompt}");

        llm.SendChatMessage(userPrompt, world,
            onSuccess: ApplyReply,
            onError: chatManager.AddChat
        );
    }

    void ApplyReply(BackendReply reply)
    {
        if (!string.IsNullOrEmpty(reply.say))
        {
            chatManager.AddChat(reply.say);
        }

        if (reply.actions == null)
        {
            return;
        }

        foreach (var action in reply.actions)
        {
            string[] parameters = action.parameters ?? Array.Empty<string>();
            string error = animationLibrary.PlayAnimation(this, action);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.Log(error);
                chatManager.AddChat(error);
            }
        }
    }
}
