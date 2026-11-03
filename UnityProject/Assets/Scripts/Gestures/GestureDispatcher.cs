using System.Collections.Generic;
using UnityEngine;

public class GestureDispatcher 
{
    readonly Dictionary<string, IGestureHandler> handlers = new();

    public void Register(IGestureHandler h) => handlers[h.Name] = h;

    public void Dispatch(GestureMarker m, AgentSystem ctx, ActionData talkData, NPC agent)
    {
        if (handlers.TryGetValue(m.Name, out var h)) agent.StartCoroutine(h.Execute(ctx, talkData, m.Args, agent));
        else Debug.LogWarning($"Unknown action {m.Name}");
    }
}
