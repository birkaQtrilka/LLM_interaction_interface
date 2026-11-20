using UnityEngine;
using UnityEngine.AI;

public static class PathReachHandler
{
    public enum Reach { Reachable, Partial, Unreachable }

    static readonly NavMeshPath tmpPath = new();

    public static Reach GetReach(NavMeshAgent agent, ContextItem item, float interactRange = 1.5f)
    {
        if (agent == null || !agent.isOnNavMesh || item.transform == null) return Reach.Unreachable;

        // Aim at the item's bounds center, falling back to the pivot
        Vector3 target = item.boundingBox.size != Vector3.zero
            ? item.boundingBox.center
            : item.transform.position;

        float sampleRadius = Mathf.Max(interactRange, item.boundingBox.extents.magnitude);
        if (!NavMesh.SamplePosition(target, out NavMeshHit hit, sampleRadius, agent.areaMask))
            return Reach.Unreachable;

        if (!agent.CalculatePath(hit.position, tmpPath)) return Reach.Unreachable;

        switch (tmpPath.status)
        {
            case NavMeshPathStatus.PathComplete: return Reach.Reachable;
            case NavMeshPathStatus.PathPartial: return Reach.Partial;
            default: return Reach.Unreachable;
        }
    }
}
