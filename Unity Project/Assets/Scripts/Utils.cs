using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public static class Utils
{
    public static IEnumerator MonitorMovement(NavMeshAgent agent, Action onDestinationReached)
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
}
