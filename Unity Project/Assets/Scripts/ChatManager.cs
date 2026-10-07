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
    Button inputBtn;

    public UnityEvent<string> OnTextSent;
    public bool CanSend => input.interactable;

    private void Awake()
    {
        input = GetComponentInChildren<TMP_InputField>();
        input.onSubmit.AddListener(OnSubmit);
        // Button OnClick is empty in the scene; Enter alone is easy to miss in Game view
        inputBtn = GetComponentInChildren<Button>();
        if (inputBtn != null) inputBtn.onClick.AddListener(() => OnSubmit(input.text));
    }

    public void SetActiveSending(bool isActive)
    {
        input.interactable =    isActive;
        if(inputBtn != null) inputBtn.interactable = isActive;
    }

    private void OnSubmit(string txt)
    {
        if (string.IsNullOrWhiteSpace(txt) || !CanSend) return;

        LookCast look = Camera.main != null ? Camera.main.GetComponent<LookCast>() : null;
        NPC nurse = null;
        if (look != null && !look.TryChoose(txt, out nurse, out string reason))
        {
            AddChat(reason);
            // Keep focus so Enter sends again after they look at someone
            input.ActivateInputField();
            return;
        }

        AddChat(nurse != null ? "to " + nurse.name + ": " + txt : "You: " + txt);
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