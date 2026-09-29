using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

static class MovementActions
{
    static readonly Dictionary<NPC, Vector3> reserved = new();

    public static AnimAction Build(NPC agent, Vector3 pos, ActionData action)
    {
        void start()
        {
            agent.Anim.SetBool("Walking", true);
            agent.Nav.SetDestination(Reserve(agent, pos, agent.Nav.radius + .2f));
        }
        IEnumerator behavior()
        {
            yield return agent.StartCoroutine(Utils.MonitorMovement(agent.Nav));
            yield return agent.StartCoroutine(TurnTowards(agent.transform, pos));
        }
        void end()
        {
            Release(agent);
            agent.Anim.SetBool("Walking", false);
            agent.Nav.ResetPath();
            agent.Nav.isStopped = false;
        }

        return new AnimAction(action, start, behavior(), end);
    }

    static IEnumerator TurnTowards(Transform t, Vector3 target, float smoothing = 3f)
    {
        // Local space offset avoids needing a subtraction, then flatten so only Y rotates
        Vector3 local = t.InverseTransformPoint(target);
        local.y = 0f;
        if (local.sqrMagnitude < 0.0001f) yield break;

        Vector3 flatDir = t.TransformDirection(local);
        Quaternion goal = Quaternion.LookRotation(flatDir, Vector3.up);

        while (Quaternion.Angle(t.rotation, goal) > 0.5f)
        {
            t.rotation = Quaternion.Slerp(t.rotation, goal, Time.deltaTime * smoothing);
            yield return null;
        }
        t.rotation = goal;
    }

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
        return target;
    }

    static bool IsTaken(NPC self, Vector3 p, float minDist)
    {
        foreach (var kv in reserved)
            if (kv.Key != self && (kv.Value - p).sqrMagnitude < minDist * minDist) return true;
        return false;
    }

    static void Release(NPC npc) => reserved.Remove(npc);
}