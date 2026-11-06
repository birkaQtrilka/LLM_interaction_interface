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

        yield return ReachAnimation(agent, spot, (t) =>
        {
            if (!started && t > .8f)
            {
                started = true;
                agent.Anim.Play("point", (int)NPC_AnimLayer.Gestures);
            }
        }, waitAfterReach: 2);
    }

    public static IEnumerator ReachAnimation(NPC agent, Transform spot, System.Action<float> onReach, float waitAfterReach = 0)
    {
        if (!agent.transform.GetChild(0).TryGetComponent<GrabIK>(out var armAnimator))
        {
            Debug.LogWarning($"Agent {agent.name} does not have a GrabIK component for arm animation.");
            yield break;
        }

        // Reuses centralized turning calculation logic
        yield return MovementActions.EnsureFacing(agent, spot.position, 40f);

        Pos startPose = armAnimator.GetCurrentHandPose();
        armAnimator.SetTargetPose(startPose.p, startPose.r);

        // Reuses kinematics offset and pointing calculations encapsulated directly in GrabIK
        Pos targetPose = armAnimator.CalculatePointPose(spot.position);

        yield return armAnimator.ArmReachPhase(startPose, targetPose, onReach);
        yield return new WaitForSeconds(waitAfterReach);

        agent.Anim.CrossFadeInFixedTime("Empty", 0.2f, (int)NPC_AnimLayer.Gestures);
        yield return armAnimator.ArmDownPhase();
    }
}