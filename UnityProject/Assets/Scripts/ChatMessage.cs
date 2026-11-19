using TMPro;
using UnityEngine;

public class ChatMessage : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI textContainer;

    public void SetText(string text)
    {
        textContainer.text = text;
    }

    // Model text can contain "<", which TMP would read as a rich text tag, and a half typed tag would flicker
    public static string Plain(string text) => "<noparse>" + text + "</noparse>";
}
