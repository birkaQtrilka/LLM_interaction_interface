using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NodGesture : IGestureHandler
{
    public string Name => "nod";

    public string Layer => "head";

    public IEnumerator Execute(AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args, NPC agent)
    {

        string name = args["isPositive"] == "true" ? "nod" : "shake";
        agent.Anim.Play(name, (int)NPC_AnimLayer.Gestures);

        return Utils.MonitorAnimatorState(agent.Anim, name, (int)NPC_AnimLayer.Gestures);
    }
}