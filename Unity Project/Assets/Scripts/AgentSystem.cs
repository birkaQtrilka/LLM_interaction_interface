using System;
using UnityEngine;

[Serializable]
public class ActionData
{
    public string name;
    public string[] parameters;
}

[Serializable]
public class ActionResponse
{
    public ActionData[] actions;
}

[Serializable]
public class ContextResponse
{
    public bool getSpots;
    public UserContext getUser;
    public ObjectContext getObjects;

    public static ContextResponse GetFullContext()
    {
        return new ContextResponse
        {
            getSpots = true,
            getUser = new UserContext
            {
                position = true,
                rotation = true,
                neighbours = true
            },
            getObjects = new ObjectContext
            {
                position = true,
                rotation = true,
                description = true,
                neighbours = true
            }
        };
    }
}

[Serializable]
public class UserContext
{
    public bool position;
    public bool rotation;
    public bool neighbours;
}

[Serializable]
public class ObjectContext
{
    public bool position;
    public bool rotation;
    public bool description;
    public bool neighbours;
}

public class AgentSystem : MonoBehaviour
{
    [SerializeField] LLMBackend llm;
    [SerializeField] AnimationLibrary animationLibrary;
    [field: SerializeField] public ContextLibrary contextLibrary { get; private set; }
    [field: SerializeField] public ChatManager chatManager { get; private set; }

    void Awake()
    {
        chatManager.OnTextSent.AddListener(OnUserMessage);
    }

    void OnUserMessage(string txt)
    {
        contextLibrary.AddMessageToHistory(txt);
        WorldSnapshot snapshot = contextLibrary.GetSnapshot();
        Debug.Log(JsonUtility.ToJson(snapshot, true));
        llm.SendChatMessage(txt, snapshot, onSuccess: ApplyReply, onError: chatManager.AddChat);
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

        foreach (ActionData action in reply.actions)
        {
            string[] parameters = action.parameters ?? Array.Empty<string>();
            string error = animationLibrary.PlayAnimation(this, action.name, parameters);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.Log(error);
                chatManager.AddChat(error);
            }
        }
    }
}
