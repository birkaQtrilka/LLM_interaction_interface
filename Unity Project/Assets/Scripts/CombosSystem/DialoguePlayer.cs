using System.Collections;
using TMPro;
using UnityEngine;

public class DialoguePlayer : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] TMP_Text title;
    [SerializeField] float delayAfter = 1f;
    [SerializeField] float charsPerSecond = 30f;
    readonly GestureDispatcher dispatcher = new();
    private void Awake()
    {
        dispatcher.Register(new NodGesture());
    }

    public IEnumerator Play(ParsedLine line, AgentSystem ctx, ActionData talkData)
    {
        gameObject.SetActive(true);
        label.text = line.CleanText;
        label.maxVisibleCharacters = 0;

        int total = line.CleanText.Length;
        int next = 0;
        float shown = 0f;
        title.text = talkData.agent;
        while (shown < total)
        {
            shown += charsPerSecond * Time.deltaTime;
            int visible = Mathf.Min(Mathf.FloorToInt(shown), total);

            while (next < line.Markers.Count && line.Markers[next].CharIndex <= visible)
                dispatcher.Dispatch(line.Markers[next++], ctx, talkData);

            label.maxVisibleCharacters = visible;
            yield return null;
        }

        while (next < line.Markers.Count)       // markers at the very end
            dispatcher.Dispatch(line.Markers[next++], ctx, talkData);
        yield return new WaitForSeconds(delayAfter);
        gameObject.SetActive(false);
    }

    public void Skip() { /* set shown to total, flush markers or drop them, your choice */ }
}