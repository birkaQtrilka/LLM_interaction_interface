using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TiltGesture : IGestureHandler
{
    public string Name => "headTilt";

    public string Layer => "head";
    private readonly int animHash = Animator.StringToHash("tilt");

    public IEnumerator Execute(AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args, NPC agent)
    {
        agent.Anim.Play(animHash, (int)NPC_AnimLayer.Gestures);
        yield return Utils.MonitorAnimatorState(agent.Anim, animHash, (int)NPC_AnimLayer.Gestures);
    }
}
