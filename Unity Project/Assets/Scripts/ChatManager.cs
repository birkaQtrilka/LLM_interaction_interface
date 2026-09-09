using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ChatManager : MonoBehaviour
{
    [SerializeField] ChatMessage messagePrefab;
    [SerializeField] Transform chatContent;
    [SerializeField] ScrollRect scrollRect;

    public string[] preparedMessages;

    TMP_InputField input;

    public UnityEvent<string> OnTextSent;

    private void Awake()
    {
        input = GetComponentInChildren<TMP_InputField>();
        input.onSubmit.AddListener(OnSubmit);
    }

    private void OnSubmit(string txt)
    {
        AddChat(txt);
        input.text = string.Empty;
        OnTextSent?.Invoke(txt);
    }

    public void AddChat(string txt)
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