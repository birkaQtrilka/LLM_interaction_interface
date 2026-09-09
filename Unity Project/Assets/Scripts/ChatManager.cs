using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatManager : MonoBehaviour
{
    [SerializeField] LLM llm;
    [SerializeField] ChatMessage messagePrefab;
    [SerializeField] Transform chatContent;
    [SerializeField] ScrollRect scrollRect;

    TMP_InputField input;

    private void Awake()
    {
        input = GetComponentInChildren<TMP_InputField>();
        input.onSubmit.AddListener(OnSubmit);
    }

    private void OnSubmit(string txt)
    {
        AddChat(txt);
        input.text = string.Empty;
        llm.SendChatMessage(txt, onSuccess: (str) => AddChat(str), onError: (str) => AddChat(str));
    }

    private void AddChat(string txt)
    {
        Instantiate(messagePrefab, chatContent).SetText(txt);

        // Forces Unity to instantly recalculate UI layout sizes (like Content Size Fitter)
        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}