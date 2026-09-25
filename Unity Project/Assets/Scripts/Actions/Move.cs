using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public static partial class Actions
{
    static readonly Dictionary<NPC, Vector3> reserved = new();

    static Vector3 Reserve(NPC npc, Vector3 target, float spacing = 1f)
    {
        Release(npc);
        for (int ring = 0; ring < 4; ring++)
        {
            int count = ring == 0 ? 1 : ring * 6;
            float radius = ring * spacing;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                var candidate = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(candidate, out var hit, spacing, NavMesh.AllAreas)) continue;
                if (IsTaken(npc, hit.position, spacing * 0.9f)) continue;

                reserved[npc] = hit.position;
                return hit.position;
            }
        }
        return target; // no free slot found, fall back to the raw target
    }

    static bool IsTaken(NPC self, Vector3 p, float minDist)
    {
        foreach (var kv in reserved)
            if (kv.Key != self && (kv.Value - p).sqrMagnitude < minDist * minDist) return true;
        return false;
    }

    static void Release(NPC npc) => reserved.Remove(npc);

    public static AnimAction Move(NPC agent, Vector3 pos, ActionData action)
    {
        void start()
        {
            agent.Anim.SetBool("Walking", true);
            agent.Nav.SetDestination(Reserve(agent, pos, agent.Nav.radius + .2f));
        }

        void end()
        {
            Release(agent);
            agent.Anim.SetBool("Walking", false);
            agent.Nav.ResetPath();
            agent.Nav.isStopped = false;
        }

        return new AnimAction(action, start, Utils.MonitorMovement(agent.Nav), end);
    }

}