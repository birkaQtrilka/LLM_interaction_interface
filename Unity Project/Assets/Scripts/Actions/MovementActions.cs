using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
static class MovementActions
{
    public static readonly Dictionary<NPC, Vector3> reserved = new();

    const float maxSightDistance = 6f;     // how far the agent can notice the item
    const float checkInterval = 0.1f;      // seconds between raycasts
    const float itemTolerance = 0.5f;      // a hit this close to pos counts as hitting the item

    public static AnimAction Build(NPC agent, TransOrPos pos, ActionData action)
    {
        LayerMask sightMask = Physics.DefaultRaycastLayers; // set this to your obstruction layers

        void start()
        {
            agent.Anim.SetBool("Walking", true);

            float standoff = agent.Nav.radius + 0.5f;
            if (pos.IsTransform && pos.transform.TryGetComponent(out NavMeshAgent other))
                standoff += other.radius;

            float startRadius = pos.IsTransform ? standoff : 0f;

            if (TryReserve(agent, pos.position, out Vector3 spot, agent.Nav.radius + 0.5f, startRadius))
                agent.Nav.SetDestination(spot);
            else
                agent.Nav.SetDestination(ApproachPoint(agent, pos.transform)); // last resort, still off the target
        }

        IEnumerator walkAndFace(float faceDistance = 2f, float turnSpeed = 360f)
        {
            var nav = agent.Nav;
            nav.updateRotation = false;

            while (nav.pathPending || nav.remainingDistance > nav.stoppingDistance + 0.05f)
            {
                Vector3 dir = nav.remainingDistance < faceDistance
                    ? pos.position - agent.transform.position   // face the item when close
                    : nav.desiredVelocity;             // otherwise face where we walk
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.001f)
                {
                    Quaternion goal = Quaternion.LookRotation(dir, Vector3.up);
                    agent.transform.rotation = Quaternion.RotateTowards(
                        agent.transform.rotation, goal, turnSpeed * Time.deltaTime);
                }
                yield return null;
            }
        }

        Coroutine lookRoutine = null;

        void startLookAt()
        {
            if (lookRoutine != null) return;
            lookRoutine = agent.StartCoroutine(LookAt(agent, pos.position));
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
            Vector3 toItem = pos.position - origin;
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

        IEnumerator behavior()
        {
            Coroutine w1 = agent.StartCoroutine(watchForItem());
            Coroutine w2 = agent.StartCoroutine(walkAndFace());
            yield return agent.StartCoroutine(Utils.MonitorMovement(agent.Nav));
            if (w1 != null) agent.StopCoroutine(w1);
            if (w2 != null) agent.StopCoroutine(w2);
        }

        void end()
        {
            Release(agent);
            StopLookAt(agent);
            agent.Anim.SetBool("Walking", false);
            agent.Nav.ResetPath();
            agent.Nav.isStopped = false;
        }

        return new AnimAction(action, start, behavior(), end);
    }

    static IEnumerator LookAt(NPC agent, Vector3 target)
    {
        LookAtIK lookAnim = agent.GetComponentInChildren<LookAtIK>();
        yield return agent.StartCoroutine(lookAnim.LookAt(target));
    }

    static void StopLookAt(NPC agent)
    {
        LookAtIK lookAnim = agent.GetComponentInChildren<LookAtIK>();
        agent.StartCoroutine(lookAnim.StopLooking());
    }


    static Vector3 ApproachPoint(NPC agent, Transform target, float gap = 0.3f)
    {
        float targetRadius = target.TryGetComponent(out NavMeshAgent other) ? other.radius : 0f;
        float standoff = agent.Nav.radius + targetRadius + gap;

        Vector3 dir = agent.transform.position - target.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude < 0.001f ? target.forward : dir.normalized;

        Vector3 p = target.position + dir * standoff;
        return NavMesh.SamplePosition(p, out var hit, 1f, NavMesh.AllAreas) ? hit.position : target.position;
    }

    public static IEnumerator TurnTowards(Transform t, Vector3 target, float smoothing = 6f)
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

    static bool TryReserve(NPC npc, Vector3 target, out Vector3 spot,
                       float spacing = 1f, float startRadius = 0f)
    {
        Release(npc);
        spot = default;

        // Project the target onto the NavMesh so a raised pivot does not break every sample
        if (NavMesh.SamplePosition(target, out var baseHit, 3f, NavMesh.AllAreas))
            target = baseHit.position;

        Vector3 from = npc.transform.position - target;
        from.y = 0f;
        float baseAngle = Mathf.Atan2(from.z, from.x);

        for (int ring = 0; ring < 4; ring++)
        {
            float radius = startRadius + ring * spacing;
            int count = Mathf.Max(6, ring * 6);

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + i * Mathf.PI * 2f / count;
                var candidate = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                if (!NavMesh.SamplePosition(candidate, out var hit, spacing, NavMesh.AllAreas))
                {
                    Debug.DrawRay(candidate, Vector3.up, Color.red, 2f);   // failed sample
                    continue;
                }
                if (IsTaken(npc, hit.position, spacing * 0.9f))
                {
                    Debug.DrawRay(hit.position, Vector3.up, Color.yellow, 2f); // taken
                    continue;
                }

                Debug.DrawRay(hit.position, Vector3.up, Color.green, 2f);   // chosen
                reserved[npc] = hit.position;
                spot = hit.position;
                return true;
            }
        }

        Debug.LogWarning($"{npc.name}: no free spot around {target}");
        return false;
    }

    static bool IsTaken(NPC self, Vector3 p, float minDist)
    {
        foreach (var kv in reserved)
            if (kv.Key != self && (kv.Value - p).sqrMagnitude < minDist * minDist) return true;
        return false;
    }

    static void Release(NPC npc) => reserved.Remove(npc);
}