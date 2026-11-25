using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TalkingHandsGesture : IGestureHandler
{
    public string Name => "talkingHands";

    public string Layer => "hands";
    private readonly int animHash = Animator.StringToHash("talkingHands");

    public IEnumerator Execute(AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args, NPC agent)
    {
        agent.Anim.Play(animHash, (int)NPC_AnimLayer.Gestures);
        yield return Utils.MonitorAnimatorState(agent.Anim, animHash, (int)NPC_AnimLayer.Gestures);
    }
}
