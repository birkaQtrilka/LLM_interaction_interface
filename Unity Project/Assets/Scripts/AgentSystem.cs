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
                neighboiurs = true
            },
            getObjects = new ObjectContext
            {
                position = true,
                rotation = true,
                description = true,
                neighboiurs = true
            }
        };
    }
}

[Serializable]
public class UserContext
{
    public bool position;
    public bool rotation;
    public bool neighboiurs;
}

[Serializable]
public class ObjectContext
{
    public bool position;
    public bool rotation;
    public bool description;
    public bool neighboiurs;
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
        llm.SendChatMessage(txt, onSuccess: chatManager.AddChat, onError: chatManager.AddChat);
    }
}
