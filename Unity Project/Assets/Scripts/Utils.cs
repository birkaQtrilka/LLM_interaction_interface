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

    public static IEnumerator MonitorAnimatorState(
        Animator animator,
        int targetStateNameHash,
        int layer = 0,
        float timeout = 5f,
        float exitAtNormalizedTime = 1f)
    {
        float deadline = Time.time + timeout;

        // Wait until the target state is active.
        while (Time.time < deadline)
        {
            var current = animator.GetCurrentAnimatorStateInfo(layer);

            if (current.shortNameHash == targetStateNameHash)
                break;

            yield return null;
        }

        if (Time.time >= deadline)
        {
            Debug.LogWarning(
                $"Timed out waiting for {targetStateNameHash} on {animator.name}"
            );
            yield break;
        }

        // Wait until the desired point in the animation.
        while (Time.time < deadline)
        {
            var current = animator.GetCurrentAnimatorStateInfo(layer);

            if (current.shortNameHash != targetStateNameHash)
                yield break;

            if (current.normalizedTime >= exitAtNormalizedTime)
                yield break;

            yield return null;
        }

        Debug.LogWarning(
            $"Timed out waiting to exit {targetStateNameHash} on {animator.name}"
        );
    }
    public static IEnumerator MonitorAnimatorState(
        Animator animator,
        string targetStateName,
        int layer = 0,
        float timeout = 5f,
        float exitAtNormalizedTime = 1f)
    {
        int hash = Animator.StringToHash(targetStateName);
        yield return MonitorAnimatorState(animator, hash, layer, timeout, exitAtNormalizedTime);
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

    public static IEnumerator OnCrEnd(this IEnumerator cr, Action callback)
    {
        if (cr != null)
        {
            yield return cr;
        }

        callback?.Invoke();
    }

    public static IEnumerator OnCrEnd(this IEnumerator cr, IEnumerator chain)
    {
        if (cr != null) yield return cr;
        if(chain != null) yield return chain;
    }
}
