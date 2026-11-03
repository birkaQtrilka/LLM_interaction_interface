using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointGesture : IGestureHandler
{
    public string Name => "point";

    public IEnumerator Execute(
        AgentSystem ctx,
        ActionData talkData,
        IReadOnlyDictionary<string, string> args)
    {
        string target = args.ContainsKey("target") ? args["target"] : null;

        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("No target specified for point gesture.");
            yield break;
        }

        Transform spot = ctx.GetObject(target)?.transform ?? ctx.GetAgent(target)?.transform;
         
        if (spot == null)
        {
            Debug.LogWarning($"Target {target} not found for point gesture.");
            yield break;
        }

        NPC agent = ctx.GetAgent(talkData.agent);
        if (agent == null) yield break;
        if (!agent.transform.GetChild(0).TryGetComponent<GrabIK>(out var armAnimator))
        {
            Debug.LogWarning($"Agent {agent.name} does not have a GrabIK component for arm animation.");
            yield break;
        }

        armAnimator.hand.GetPositionAndRotation(
            out Vector3 startPos,
            out Quaternion startRot
        );

        armAnimator.SetTargetPose(startPos, startRot);
        Transform shoulder = armAnimator.UpperArm;
        Vector3 startDir = (startPos - shoulder.position).normalized;
        Vector3 targetDir = (spot.position - shoulder.position).normalized;
        Quaternion armSwing = Quaternion.FromToRotation(
            startDir,
            targetDir
        );
        Quaternion targetRot = armSwing * startRot;
        float armLength = Vector3.Distance(shoulder.position, armAnimator.hand.position);

        Vector3 targetPos = shoulder.position + targetDir * (armLength-.1f);
        bool started = false;
        yield return armAnimator.ArmReachPhase(
            agent.pointProfile,
            new Pos(startPos, startRot),
            new Pos(targetPos, Quaternion.identity),
            (t)=>
            {
                if(!started && t > .8)
                {
                    started = true;
                    agent.Anim.Play(Name, layer: 2);
                }
            }
        );
        yield return new WaitForSeconds(2);

        yield return armAnimator.ArmDownPhase(
            agent.pointProfile
        );
    }
}