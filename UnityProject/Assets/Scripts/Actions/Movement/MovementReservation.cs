using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static class MovementReservation
{
    public static readonly Dictionary<NPC, Vector3> Reserved = new();

    public static Vector3 ApproachPoint(NPC agent, Transform target, float gap = 0.3f)
    {
        if (target == null) return agent.transform.position;

        float targetRadius = target.TryGetComponent(out NavMeshAgent other) ? other.radius : 0f;
        float minDistance = agent.Nav.radius + targetRadius;
        float standoff = minDistance + gap;

        Vector3 toAgent = agent.transform.position - target.position;
        toAgent.y = 0f;
        float currentDist = toAgent.magnitude;

        if (currentDist <= standoff && currentDist >= minDistance)
            return agent.transform.position;

        Vector3 dir = (currentDist < 0.01f) ? -agent.transform.forward : toAgent / currentDist;
        Vector3 p = target.position + dir * standoff;
        return NavMesh.SamplePosition(p, out var hit, 1f, NavMesh.AllAreas) ? hit.position : target.position;
    }

    public static bool TryReserve(NPC npc, Vector3 target, out Vector3 spot,
                                  float spacing = 1f, float startRadius = 0f)
    {
        Release(npc);
        spot = default;

        if (NavMesh.SamplePosition(target, out var baseHit, 3f, NavMesh.AllAreas))
            target = baseHit.position;

        Vector3 from = npc.transform.position - target;
        from.y = 0f;

        float baseAngle = from.sqrMagnitude > 0.001f
            ? Mathf.Atan2(from.z, from.x)
            : Mathf.Atan2(-npc.transform.forward.z, -npc.transform.forward.x);

        if (startRadius > 0f && from.magnitude <= startRadius && from.magnitude > npc.Nav.radius)
        {
            if (!IsTaken(npc, npc.transform.position, spacing * 0.9f))
            {
                Reserved[npc] = npc.transform.position;
                spot = npc.transform.position;
                return true;
            }
        }

        for (int ring = 0; ring < 4; ring++)
        {
            float radius = startRadius + ring * spacing;
            int count = Mathf.Max(6, ring * 6);

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + i * Mathf.PI * 2f / count;
                var candidate = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                if (!NavMesh.SamplePosition(candidate, out var hit, spacing, NavMesh.AllAreas))
                    continue;

                if (IsTaken(npc, hit.position, spacing * 0.9f))
                    continue;

                Reserved[npc] = hit.position;
                spot = hit.position;
                return true;
            }
        }

        Debug.LogWarning($"{npc.name}: no free spot around {target}");
        return false;
    }

    public static bool IsTaken(NPC self, Vector3 p, float minDist)
    {
        foreach (var kv in Reserved)
        {
            if (kv.Key != self && (kv.Value - p).sqrMagnitude < minDist * minDist)
                return true;
        }
        return false;
    }

    public static void Release(NPC npc) => Reserved.Remove(npc);
}