using System.Collections.Generic;
using UnityEngine;

public class GestureDispatcher 
{
    readonly Dictionary<string, IGestureHandler> handlers = new();

    public void Register(IGestureHandler h) => handlers[h.Name] = h;

    public void Dispatch(GestureMarker m, AgentSystem ctx, ActionData talkData)
    {
        if (handlers.TryGetValue(m.Name, out var h)) ctx.StartCoroutine(h.Execute(ctx, talkData, m.Args));
        else Debug.LogWarning($"Unknown action {m.Name}");
    }
}
