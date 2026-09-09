using TMPro;
using UnityEngine;

public class ChatMessage : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textContainer;

    public void SetText(string text)
    {
        textContainer.text = text;
    }
}
