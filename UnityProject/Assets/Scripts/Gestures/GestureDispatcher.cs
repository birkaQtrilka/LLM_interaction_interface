using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GestureDispatcher
{
    readonly Dictionary<string, IGestureHandler> handlers = new();
    readonly HashSet<string> busyLayers = new();

    public void Register(IGestureHandler h) => handlers[h.Name] = h;

    public void Dispatch(GestureMarker m, AgentSystem ctx, ActionData talkData, NPC agent)
    {
        if (!handlers.TryGetValue(m.Name, out var h))
        {
            Debug.LogWarning($"Unknown gesture {m.Name}");
            return;
        }

        // Add returns false if the layer is already in the set
        if (!busyLayers.Add(h.Layer))
        {
            Debug.LogWarning($"gesture {m.Name} won't be played because there is already one playing on layer: {h.Layer}");
            return;
        }

        agent.StartCoroutine(RunGesture(h, ctx, talkData, m.Args, agent));
    }

    IEnumerator RunGesture(IGestureHandler h, AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args, NPC agent)
    {
        try
        {
            yield return h.Execute(ctx, talkData, args, agent); // runs the handler as a nested coroutine
        }
        finally
        {
            busyLayers.Remove(h.Layer); // runs on completion or if the handler throws
        }
    }
}