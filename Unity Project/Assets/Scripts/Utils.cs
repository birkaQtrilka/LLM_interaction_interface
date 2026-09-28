using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public static class Utils
{
    public static IEnumerator MonitorMovement(NavMeshAgent agent, Action onDestinationReached = null, Action onMove = null)
    {
        yield return new WaitUntil(() => !agent.pathPending);

        while (IsAgentMoving(agent) || agent.isStopped)
        {
            if (!agent.isStopped) onMove?.Invoke();
            yield return null;
        }

        onDestinationReached?.Invoke();
    }

    // A helper method you can use anywhere to check if an agent is moving
    public static bool IsAgentMoving(NavMeshAgent agent)
    {
        if (agent.pathPending) return true;

        if (agent.remainingDistance > agent.stoppingDistance) return true;

        if (agent.hasPath && agent.velocity.sqrMagnitude > 0.001f) return true;

        return false;
    }

    public static float GetClipLength(Animator animator, string clipName)
    {
        var controller = animator.runtimeAnimatorController;
        if (controller == null) return 0f;

        foreach (var clip in controller.animationClips)
        {
            if (clip.name == clipName) return clip.length;
        }

        Debug.LogWarning($"No clip named {clipName} on {animator.name}");
        return 0f;
    }

    public static IEnumerator MonitorAnimatorState(Animator animator, string targetStateName, int layer = 0, float timeout = 5f)
    {
        int hash = Animator.StringToHash(targetStateName);
        float deadline = Time.time + timeout;

        // 1. Wait to ENTER the target state (or start transitioning to it)
        // This solves the 1-frame delay issue of SetTrigger.
        while (true)
        {
            if (Time.time > deadline)
            {
                Debug.LogWarning($"Timed out waiting to enter state {targetStateName} on {animator.name}");
                yield break;
            }

            var currentInfo = animator.GetCurrentAnimatorStateInfo(layer);
            var nextInfo = animator.GetNextAnimatorStateInfo(layer);

            if (currentInfo.shortNameHash == hash || nextInfo.shortNameHash == hash)
            {
                break;
            }

            yield return null;
        }

        // 2. Wait to EXIT the target state (or for the animation to finish)
        while (true)
        {
            var currentInfo = animator.GetCurrentAnimatorStateInfo(layer);
            var nextInfo = animator.GetNextAnimatorStateInfo(layer);

            // Condition A: We are currently in the state, but it has finished playing (for non-looping animations)
            if (currentInfo.shortNameHash == hash && currentInfo.normalizedTime >= 1f && !animator.IsInTransition(layer))
                break;

            // Condition B: We are transitioning OUT of the target state to a different state
            if (animator.IsInTransition(layer) && nextInfo.shortNameHash != hash)
                break;

            // Condition C: Something else forcibly overrode the state completely
            if (currentInfo.shortNameHash != hash && !animator.IsInTransition(layer))
                break;

            yield return null;
        }
    }

    public static IEnumerator MonitorFlag(Flag flag, float timeout = 5f)
    {
        // Give the Animator a frame to process the SetTrigger
        yield return null;

        while (!flag.value && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        Debug.Assert(timeout > 0);
    }
}
