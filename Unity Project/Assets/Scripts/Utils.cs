using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public static class Utils
{
    public static IEnumerator MonitorMovement(NavMeshAgent agent, Action onDestinationReached = null)
    {
        yield return new WaitUntil(() => !agent.pathPending);

        while (IsAgentMoving(agent))
        {
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

    public static IEnumerator MonitorAnimatorState(Animator animator, string stateName, int layer = 0, float timeout = 5f)
    {
        int hash = Animator.StringToHash(stateName);
        float deadline = Time.time + timeout;

        // wait for the trigger to be consumed and the transition into the state to finish
        while (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash != hash)
        {
            if (Time.time > deadline)
            {
                Debug.LogWarning($"Timed out waiting for state {stateName} on {animator.name}");
                yield break;
            }
            yield return null;
        }

        // now play it through once
        while (true)
        {
            var info = animator.GetCurrentAnimatorStateInfo(layer);
            if (info.shortNameHash != hash) break;            // something else took over
            if (animator.IsInTransition(layer)) break;         // transitioning out
            if (info.normalizedTime >= 1f) break;              // finished a full pass
            yield return null;
        }
    }
}
