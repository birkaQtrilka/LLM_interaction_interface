using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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