using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

static class MovementActions
{
    public static readonly Dictionary<NPC, Vector3> reserved = new();

    const float maxSightDistance = 6f;
    const float checkInterval = 0.1f;
    const float itemTolerance = 0.5f;

    // Animator parameter hashes (Rotation removed)
    static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    static readonly int VerticalHash = Animator.StringToHash("Vertical");
    static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    //static readonly int TurnAngleHash = Animator.StringToHash("TurnAngle");
    //static readonly int DoTurnHash = Animator.StringToHash("DoTurn");

    public static AnimAction Build(NPC agent, TransOrPos pos, ActionData action)
    {
        LayerMask sightMask = Physics.DefaultRaycastLayers;

        void start()
        {
            float standoff = agent.Nav.radius + 0.5f;
            if (pos.IsTransform && pos.transform.TryGetComponent(out NavMeshAgent other))
                standoff += other.radius;

            float startRadius = pos.IsTransform ? standoff : 0f;

            if (TryReserve(agent, pos.position, out Vector3 spot, agent.Nav.radius + 0.5f, startRadius))
                agent.Nav.SetDestination(spot);
            else
                agent.Nav.SetDestination(ApproachPoint(agent, pos.transform));
        }

        IEnumerator walkAndFace()
        {
            float baseWalkSpeed = agent.WalkSpeedMult; // Natural clip speed for foot sync
            float turnSpeed = agent.TurnSpeed;      // Degrees per second the script rotates the NPC
            float maxTurnAngle = agent.MaxTurnAngle;    // Angle that produces maximum h (1.0 or -1.0)
            float faceDistance = agent.FaceDistance;     // Distance to item where we start facing the item
            float dampTime = agent.DampTime;

            var nav = agent.Nav;
            nav.updateRotation = false;  // We handle rotation manually below
            Vector3 initialDir = nav.desiredVelocity;
            initialDir.y = 0f;

            if (initialDir.sqrMagnitude > 0.001f)
            {
                initialDir.Normalize();

                float turnAngle = Vector3.SignedAngle(
                    agent.transform.forward,
                    initialDir,
                    Vector3.up
                );

                if (Mathf.Abs(turnAngle) > 60f)
                {
                    nav.isStopped = true;

                    //agent.Anim.SetFloat(TurnAngleHash, turnAngle);
                    //agent.Anim.SetTrigger(DoTurnHash);

                    Quaternion targetRot = Quaternion.LookRotation(
                        initialDir,
                        Vector3.up
                    );

                    while (Quaternion.Angle(agent.transform.rotation, targetRot) > 1f)
                    {
                        agent.transform.rotation = Quaternion.RotateTowards(
                            agent.transform.rotation,
                            targetRot,
                            turnSpeed * Time.deltaTime
                        );

                        yield return null;
                    }

                    nav.isStopped = false;
                }
            }

            while (nav.pathPending || nav.remainingDistance > nav.stoppingDistance + 0.05f)
            {
                Vector3 targetDir = nav.remainingDistance < faceDistance
                    ? pos.position - agent.transform.position
                    : nav.desiredVelocity;
                targetDir.y = 0f;

                float h = 0f;
                float v = 0f;

                if (targetDir.sqrMagnitude > 0.001f)
                {
                    Vector3 normTarget = targetDir.normalized;

                    Quaternion goalRot = Quaternion.LookRotation(normTarget, Vector3.up);
                    agent.transform.rotation = Quaternion.RotateTowards(
                        agent.transform.rotation, goalRot, turnSpeed * Time.deltaTime);

                    // 3. Calculate H based on angle difference
                    // Angle > 0 means target is to the right (h -> +1)
                    // Angle < 0 means target is to the left (h -> -1)
                    float angle = Vector3.SignedAngle(agent.transform.forward, normTarget, Vector3.up);
                    h = Mathf.Clamp(angle / maxTurnAngle, -1f, 1f);

                    // 4. Calculate V (Forward Walk)
                    // If the agent is facing mostly away (> 90 deg turn), slow down V until
                    // it turns around so it doesn't moonwalk sideways/backward.
                    float alignment = Mathf.Max(0f, Vector3.Dot(agent.transform.forward, normTarget));
                    v = alignment; // Drops towards 0 during sharp turns, hits 1.0 when aligned
                }

                float currentSpeed = nav.velocity.magnitude;
                float motionSpeed = (baseWalkSpeed > 0.001f) ? (currentSpeed / baseWalkSpeed) : 1f;
                if (currentSpeed > 0.05f)
                    motionSpeed = Mathf.Max(motionSpeed, 0.5f);

                agent.Anim.SetFloat(HorizontalHash, h, dampTime, Time.deltaTime);
                agent.Anim.SetFloat(VerticalHash, v, dampTime, Time.deltaTime);
                agent.Anim.SetFloat(MotionSpeedHash, motionSpeed, dampTime, Time.deltaTime);

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
            StopLookAt(agent);
        }

        bool canSeeItem()
        {
            Vector3 origin = agent.Head.position;
            Vector3 toItem = pos.position - origin;
            float dist = toItem.magnitude;

            if (dist > maxSightDistance) return false;
            if (dist < 0.01f) return true;

            if (Physics.Raycast(origin, toItem / dist, out RaycastHit hit, dist, sightMask, QueryTriggerInteraction.Ignore))
                return hit.distance + itemTolerance >= dist;

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

            if (w2 != null) agent.StopCoroutine(w2);

            yield return agent.StartCoroutine(BlendToIdle(agent));

            if (w1 != null) agent.StopCoroutine(w1);
        }

        void end()
        {
            Release(agent);
            StopLookAt(agent);

            agent.Anim.SetFloat(HorizontalHash, 0f);
            agent.Anim.SetFloat(VerticalHash, 0f);
            agent.Anim.SetFloat(MotionSpeedHash, 1f);

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
    static IEnumerator BlendToIdle(NPC agent)
    {
        var anim = agent.Anim;
        float dampTime = agent.DampTime;

        // Smoothly return to the idle blend-tree values.
        while (Mathf.Abs(anim.GetFloat(HorizontalHash)) > 0.01f ||
               Mathf.Abs(anim.GetFloat(VerticalHash)) > 0.01f ||
               Mathf.Abs(anim.GetFloat(MotionSpeedHash) - 1f) > 0.01f)
        {
            anim.SetFloat(HorizontalHash, 0f, dampTime, Time.deltaTime);
            anim.SetFloat(VerticalHash, 0f, dampTime, Time.deltaTime);
            anim.SetFloat(MotionSpeedHash, 1f, dampTime, Time.deltaTime);

            yield return null;
        }

        // Ensure the final values are exact.
        anim.SetFloat(HorizontalHash, 0f);
        anim.SetFloat(VerticalHash, 0f);
        anim.SetFloat(MotionSpeedHash, 1f);
    }
    static bool TryReserve(NPC npc, Vector3 target, out Vector3 spot,
                       float spacing = 1f, float startRadius = 0f)
    {
        Release(npc);
        spot = default;

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
                    continue;

                if (IsTaken(npc, hit.position, spacing * 0.9f))
                    continue;

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