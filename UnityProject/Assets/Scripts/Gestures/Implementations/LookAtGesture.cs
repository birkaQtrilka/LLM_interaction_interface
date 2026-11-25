using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtGesture : IGestureHandler
{
    public string Name => "lookAt";

    public string Layer => "head";
    public const float DefaultDuration = 1.0f; // Default duration for looking at the target
    public IEnumerator Execute(AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args, NPC agent)
    {
        if(!args.TryGetValue("target", out string targetName)) {
            Debug.LogWarning("LookAtGesture: 'target' argument is missing.");
            yield break;
        }
        Transform target = ctx.GetAny(targetName);
        if (target == null) { 
            Debug.LogWarning($"LookAtGesture: target '{targetName}' not found.");
            yield break;
        }

        LookHandle handle = agent.Look.Request(() => target.position);

        if(args.TryGetValue("duration", out string durationStr) && float.TryParse(durationStr, out float duration)) {
            yield return new WaitForSeconds(duration);
        } else {
            yield return new WaitForSeconds(DefaultDuration);
        }
        agent.Look.Release(handle);
    }
}
