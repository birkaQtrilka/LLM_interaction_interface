using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointGesture : IGestureHandler
{
    public string Name => "point";

    public IEnumerator Execute(
        AgentSystem ctx,
        ActionData talkData,
        IReadOnlyDictionary<string, string> args,
        NPC agent)
    {
        string target = args.ContainsKey("target") ? args["target"] : null;
        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("No target specified for point gesture.");
            yield break;
        }
        bool started = false;
        Transform spot = ctx.GetObject(target)?.transform ?? ctx.GetAgent(target)?.transform;
        if (spot == null)
        {
            Debug.LogWarning($"Target {target} not found for point gesture.");
            yield break;
        }

        yield return ReachAnimation(agent, spot, ctx,
            (t) =>
            {
                if (!started && t > .8f)
                {
                    started = true;
                    agent.Anim.Play("point", (int)NPC_AnimLayer.Gestures);
                }
            }
            , waitAfterReach: 2);
    }

    public static IEnumerator ReachAnimation(NPC agent, Transform spot, AgentSystem ctx, System.Action<float> onReach,  float waitAfterReach = 0)
    {
        if (!agent.transform.GetChild(0).TryGetComponent<GrabIK>(out var armAnimator))
        {
            Debug.LogWarning($"Agent {agent.name} does not have a GrabIK component for arm animation.");
            yield break;
        }

        Transform shoulder = armAnimator.UpperArm;
        Vector3 flatDir = spot.position - agent.transform.position;
        flatDir.y = 0f;

        if (flatDir.sqrMagnitude > 0.0001f)
        {
            yield return MovementActions.WaitForTurn(agent);

            yield return null;
            while (agent.Anim.IsInTransition(0))
                yield return null;

            float turnAngle = Vector3.SignedAngle(agent.transform.forward, flatDir, Vector3.up);
            if (Mathf.Abs(turnAngle) > 40f)
                yield return MovementActions.StartTurn(agent, spot.position);
        }

        armAnimator.hand.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        armAnimator.SetTargetPose(startPos, startRot);

        Vector3 startDir = (startPos - shoulder.position).normalized;
        Vector3 targetDir = (spot.position - shoulder.position).normalized;

        Vector3 targetUp = Vector3.up;

        Quaternion targetRot = armAnimator.GetHandRotationFacing(targetDir, targetUp);

        float armLength = Vector3.Distance(shoulder.position, armAnimator.hand.position);
        Vector3 targetPos = shoulder.position + targetDir * (armLength - 0.1f);

        yield return armAnimator.ArmReachPhase(
            agent.pointProfile,
            new Pos(startPos, startRot),
            new Pos(targetPos, targetRot),
            onReach
        );
        yield return new WaitForSeconds(waitAfterReach);
        agent.Anim.CrossFadeInFixedTime("Empty", 0.2f, (int)NPC_AnimLayer.Gestures);

        yield return armAnimator.ArmDownPhase(
            agent.pointProfile
        );
    }
}