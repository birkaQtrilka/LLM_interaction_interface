using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

static class MovementActions
{
    static readonly Dictionary<NPC, Vector3> reserved = new();

    public static AnimAction Build(NPC agent, Vector3 pos, ActionData action)
    {
        const float maxSightDistance = 6f;     // how far the agent can notice the item
        const float checkInterval = 0.1f;      // seconds between raycasts
        const float itemTolerance = 0.5f;      // a hit this close to pos counts as hitting the item
        LayerMask sightMask = Physics.DefaultRaycastLayers; // set this to your obstruction layers

        Coroutine lookRoutine = null;

        void startLookAt()
        {
            if (lookRoutine != null) return;
            lookRoutine = agent.StartCoroutine(LookAt(agent, pos));
        }

        void stopLookAt()
        {
            if (lookRoutine == null) return;
            agent.StopCoroutine(lookRoutine);
            lookRoutine = null;
            StopLookAt(agent); // your graceful blend out
        }

        bool canSeeItem()
        {
            Vector3 origin = agent.Head.position;
            Vector3 toItem = pos - origin;
            float dist = toItem.magnitude;

            if (dist > maxSightDistance) return false;
            if (dist < 0.01f) return true;

            if (Physics.Raycast(origin, toItem / dist, out RaycastHit hit, dist, sightMask, QueryTriggerInteraction.Ignore))
            {
                // Something was hit before reaching pos. Only counts as visible if it is basically the item itself
                return hit.distance + itemTolerance >= dist;
            }
            return true;
        }

        IEnumerator watchForItem()
        {
            var wait = new WaitForSeconds(checkInterval);
            while (true)
            {
                if (canSeeItem()) startLookAt();
                else stopLookAt();
                yield return wait;
            }
        }

        void start()
        {
            agent.Anim.SetBool("Walking", true);
            agent.Nav.SetDestination(Reserve(agent, pos, agent.Nav.radius + .2f));
        }

        IEnumerator behavior()
        {
            Coroutine watcher = agent.StartCoroutine(watchForItem());

            yield return agent.StartCoroutine(Utils.MonitorMovement(agent.Nav));

            yield return agent.StartCoroutine(TurnTowards(agent.transform, pos));
            agent.StopCoroutine(watcher);
        }

        void end()
        {
            Release(agent);
            stopLookAt(); // covers the case where the action is interrupted mid walk
            agent.Anim.SetBool("Walking", false);
            agent.Nav.ResetPath();
            agent.Nav.isStopped = false;
        }

        return new AnimAction(action, start, behavior(), end);
    }

    // Placeholders for your existing logic
    static IEnumerator LookAt(NPC agent, Vector3 target)
    {
        // your look at logic here
        LookAtIK lookAnim = agent.GetComponentInChildren<LookAtIK>();
        yield return agent.StartCoroutine(lookAnim.LookAt(target));
    }

    static void StopLookAt(NPC agent)
    {
        LookAtIK lookAnim = agent.GetComponentInChildren<LookAtIK>();
        agent.StartCoroutine(lookAnim.StopLooking());
    }

    static IEnumerator TurnTowards(Transform t, Vector3 target, float smoothing = 6f)
    {
        // Local space offset avoids needing a subtraction, then flatten so only Y rotates
        Vector3 local = t.InverseTransformPoint(target);
        local.y = 0f;
        if (local.sqrMagnitude < 0.0001f) yield break;

        Vector3 flatDir = t.TransformDirection(local);
        Quaternion goal = Quaternion.LookRotation(flatDir, Vector3.up);

        while (Quaternion.Angle(t.rotation, goal) > 0.5f)
        {
            t.rotation = Quaternion.Lerp(t.rotation, goal, Time.deltaTime * smoothing);
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