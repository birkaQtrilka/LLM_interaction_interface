using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Types a spoken line into the chat letter by letter and fires its gestures when the typing reaches them
/// </summary>
public class DialoguePlayer : MonoBehaviour
{
    [SerializeField] float charsPerSecond = 30f;
    readonly GestureDispatcher dispatcher = new();

    private void Awake()
    {
        RegisterAllGestures();
    }

    private void RegisterAllGestures()
    {
        Type gestureBaseType = typeof(IGestureHandler);

        var gestureTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => gestureBaseType.IsAssignableFrom(t)
                     && !t.IsInterface
                     && !t.IsAbstract
                     && t.GetConstructor(Type.EmptyTypes) != null);

        foreach (var type in gestureTypes)
        {
            var gestureInstance = (IGestureHandler)Activator.CreateInstance(type);
            dispatcher.Register(gestureInstance);
        }
    }

    // header is the "Speaker to receiver: " part, shown at once so the reader knows who talks
    public IEnumerator Play(ParsedLine line, AgentSystem ctx, ActionData talkData, NPC agent, string header)
    {
        ChatMessage message = null;
        if (ctx.chatManager == null)
            Debug.LogWarning("No ChatManager on AgentSystem, the line plays its gestures without being shown");
        else
            message = ctx.chatManager.AddChat(header);

        string text = line.CleanText;
        int total = text.Length;
        int next = 0;
        int lastVisible = -1;
        float shown = 0f;
        while (shown < total)
        {
            shown += charsPerSecond * Time.deltaTime;
            int visible = Mathf.Min(Mathf.FloorToInt(shown), total);

            while (next < line.Markers.Count && line.Markers[next].CharIndex <= visible)
                dispatcher.Dispatch(line.Markers[next++], ctx, talkData, agent);

            // The text itself grows (not maxVisibleCharacters), so the bubble height follows the typed part
            if (message != null && visible != lastVisible)
            {
                message.SetText(header + ChatMessage.Plain(text.Substring(0, visible)));
                ctx.chatManager.ScrollToBottom();
                lastVisible = visible;
            }
            yield return null;
        }

        while (next < line.Markers.Count)       // markers at the very end
            dispatcher.Dispatch(line.Markers[next++], ctx, talkData, agent);

        if (message != null)
        {
            // Covers an empty line, where the loop above never runs
            message.SetText(header + ChatMessage.Plain(text));
            ctx.chatManager.ScrollToBottom();
        }
    }
}
