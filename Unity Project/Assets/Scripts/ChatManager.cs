using System;
using TMPro;
using UnityEngine;

public class ChatManager : MonoBehaviour
{
    [SerializeField] LLM llm;
    [SerializeField] ChatMessage messagePrefab;
    [SerializeField] Transform chatContent;
    TMP_InputField input;

    private void Awake()
    {
        input = GetComponentInChildren<TMP_InputField>();
        input.onSubmit.AddListener(OnSubmit);
    }

    private void OnSubmit(string txt)
    {
        AddChat(txt);
        llm.SendChatMessage(txt, onSuccess: (str) => AddChat(str), onError: (str) => AddChat(str));
    }

    private void AddChat(string txt)
    {
        Instantiate(messagePrefab, chatContent).SetText(txt);
        Canvas.ForceUpdateCanvases();
    }
}
