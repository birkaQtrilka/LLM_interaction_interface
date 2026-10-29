using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGestureHandler
{
    string Name { get; }
    IEnumerator Execute(AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args);
}

public class GestureDispatcher 
{
    readonly Dictionary<string, IGestureHandler> handlers = new();

    public void Register(IGestureHandler h) => handlers[h.Name] = h;

    public void Dispatch(GestureMarker m, AgentSystem ctx, ActionData talkData)
    {
        if (handlers.TryGetValue(m.Name, out var h)) h.Execute(ctx, talkData, m.Args);
        else Debug.LogWarning($"Unknown action {m.Name}");
    }
}

public class NodGesture : IGestureHandler
{
    public string Name => "nod";

    public IEnumerator Execute(AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args)
    {
        NPC agent = ctx.GetAgent(talkData.agent);
        agent.Anim.Play(Name, layer: 2);
        Debug.Log("Executing nod gesture");
        return Utils.MonitorAnimatorState(agent.Anim, Name, layer: 2);
    }
}