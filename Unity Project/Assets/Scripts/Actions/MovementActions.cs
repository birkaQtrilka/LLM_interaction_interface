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

    static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    static readonly int VerticalHash = Animator.StringToHash("Vertical");
    static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    static readonly int TurnAngleHash = Animator.StringToHash("TurnAngle");
    static readonly int DoTurnHash = Animator.StringToHash("DoTurn");
    static readonly int MoveHash = Animator.StringToHash("Move");
    static readonly int TurnInPlaceHash = Animator.StringToHash("TurnInPlace");

    public static AnimAction Build(NPC agent, TransOrPos pos, ActionData action)
    {
        LayerMask sightMask = Physics.DefaultRaycastLayers;
        Coroutine lookRoutine = null;
        Coroutine watchRoutine = null;
        Coroutine walkRoutine = null;

        void start()
        {
            float standoff = agent.Nav.radius + 0.5f;
            if (pos.IsTransform && pos.transform != null && pos.transform.TryGetComponent(out NavMeshAgent other))
                standoff += other.radius;

            float startRadius = pos.IsTransform ? standoff : 0f;

            Vector3 destination;
            if (TryReserve(agent, pos.position, out Vector3 spot, agent.Nav.radius + 0.5f, startRadius))
                destination = spot;
            else if (pos.IsTransform)
                destination = ApproachPoint(agent, pos.transform);
            else
                destination = pos.position;

            if (Vector3.Distance(agent.transform.position, destination) > agent.Nav.stoppingDistance + 0.05f)
                agent.Nav.SetDestination(destination);
        }

        IEnumerator walkAndFace()
        {
            var nav = agent.Nav;

            Vector3 initialDir = (nav.steeringTarget - agent.transform.position);
            initialDir.y = 0f;

            if (initialDir.sqrMagnitude > 0.001f)
            {
                initialDir.Normalize();
                float turnAngle = Vector3.SignedAngle(agent.transform.forward, initialDir, Vector3.up);

                if (Mathf.Abs(turnAngle) > agent.MaxTurnAngle)
                {
                    nav.isStopped = true;
                    agent.Anim.SetFloat(TurnAngleHash, turnAngle);
                    agent.Anim.SetTrigger(DoTurnHash);

                    Quaternion targetRot = Quaternion.LookRotation(initialDir, Vector3.up);
                    yield return Utils.MonitorAnimatorState(agent.Anim, TurnInPlaceHash);

                    AnimatorStateInfo state = agent.Anim.GetCurrentAnimatorStateInfo(0);
                    agent.Anim.Play(state.fullPathHash, 0, 0f);
                    agent.Anim.Update(0f);
                    agent.Anim.CrossFade(MoveHash, 0.35f, 0);
                    agent.transform.rotation = targetRot;
                    nav.isStopped = false;
                }
            }

            while (nav.remainingDistance > nav.stoppingDistance + 0.05f)
            {
                Vector3 targetDir = nav.remainingDistance < agent.FaceDistance
                    ? pos.position - agent.transform.position
                    : nav.desiredVelocity;
                targetDir.y = 0f;

                float h = 0f;
                float v = 0f;

                if (targetDir.sqrMagnitude > 0.001f)
                {
                    Vector3 normTarget = targetDir.normalized;

                    float angle = Vector3.SignedAngle(agent.transform.forward, normTarget, Vector3.up);
                    h = Mathf.Clamp(angle / agent.MaxTurnAngle, -1f, 1f);

                    float alignment = Mathf.Max(0f, Vector3.Dot(agent.transform.forward, normTarget));
                    v = alignment;
                }

                float currentSpeed = nav.velocity.magnitude;
                float motionSpeed = (agent.WalkSpeedMult > 0.001f) ? (currentSpeed / agent.WalkSpeedMult) : 1f;
                if (currentSpeed > 0.05f)
                    motionSpeed = Mathf.Max(motionSpeed, 0.5f);

                agent.Anim.SetFloat(HorizontalHash, h, agent.DampTime, Time.deltaTime);
                agent.Anim.SetFloat(VerticalHash, v, agent.DampTime, Time.deltaTime);
                agent.Anim.SetFloat(MotionSpeedHash, motionSpeed, agent.DampTime, Time.deltaTime);

                yield return null;
            }
        }

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

            // Wait for NavMesh to finish asynchronous path calculation
            yield return null;
            while (agent.Nav.pathPending)
                yield return null;
            watchRoutine = agent.StartCoroutine(watchForItem());

            // If already in range or no path needed, just turn to face the target
            if (!agent.Nav.hasPath || agent.Nav.remainingDistance <= agent.Nav.stoppingDistance + 0.05f)
            {
                yield return agent.StartCoroutine(TurnTowards(agent, pos.position));
            }
            else
            {
                walkRoutine = agent.StartCoroutine(walkAndFace());
                yield return agent.StartCoroutine(Utils.MonitorMovement(agent.Nav));
                if (walkRoutine != null)
                {
                    agent.StopCoroutine(walkRoutine);
                    walkRoutine = null;
                }
            }

            yield return agent.StartCoroutine(BlendToIdle(agent));
        }

        void end()
        {
            Release(agent);

            if (watchRoutine != null)
            {
                agent.StopCoroutine(watchRoutine);
                watchRoutine = null;
            }

            if (walkRoutine != null)
            {
                agent.StopCoroutine(walkRoutine);
                walkRoutine = null;
            }

            stopLookAt();

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
        if (lookAnim != null)
            yield return agent.StartCoroutine(lookAnim.LookAt(target));
    }

    static void StopLookAt(NPC agent)
    {
        LookAtIK lookAnim = agent.GetComponentInChildren<LookAtIK>();
        if (lookAnim != null)
            agent.StartCoroutine(lookAnim.StopLooking());
    }

    static Vector3 ApproachPoint(NPC agent, Transform target, float gap = 0.3f)
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

    public static IEnumerator TurnTowards(NPC agent, Vector3 target)
    {
        Transform t = agent.transform;
        NavMeshAgent nav = agent.Nav;
        Vector3 local = t.InverseTransformPoint(target);
        local.y = 0f;
        if (local.sqrMagnitude < 0.0001f) yield break;

        Vector3 flatDir = t.TransformDirection(local);
        float turnAngle = Vector3.SignedAngle(agent.transform.forward, flatDir, Vector3.up);

        nav.isStopped = true;
        agent.Anim.SetFloat(TurnAngleHash, turnAngle);
        agent.Anim.SetTrigger(DoTurnHash);

        Quaternion targetRot = Quaternion.LookRotation(flatDir, Vector3.up);
        yield return Utils.MonitorAnimatorState(agent.Anim, TurnInPlaceHash);

        AnimatorStateInfo state = agent.Anim.GetCurrentAnimatorStateInfo(0);
        agent.Anim.Play(state.fullPathHash, 0, 0f);
        agent.Anim.Update(0f);
        agent.Anim.CrossFade(MoveHash, 0.35f, 0);
        agent.transform.rotation = targetRot;
        nav.isStopped = false;
    }

    static IEnumerator BlendToIdle(NPC agent)
    {
        var anim = agent.Anim;
        float dampTime = agent.DampTime;

        while (Mathf.Abs(anim.GetFloat(HorizontalHash)) > 0.01f ||
               Mathf.Abs(anim.GetFloat(VerticalHash)) > 0.01f ||
               Mathf.Abs(anim.GetFloat(MotionSpeedHash) - 1f) > 0.01f)
        {
            anim.SetFloat(HorizontalHash, 0f, dampTime, Time.deltaTime);
            anim.SetFloat(VerticalHash, 0f, dampTime, Time.deltaTime);
            anim.SetFloat(MotionSpeedHash, 1f, dampTime, Time.deltaTime);
            yield return null;
        }

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

        float baseAngle = from.sqrMagnitude > 0.001f
            ? Mathf.Atan2(from.z, from.x)
            : Mathf.Atan2(-npc.transform.forward.z, -npc.transform.forward.x);

        if (startRadius > 0f && from.magnitude <= startRadius && from.magnitude > npc.Nav.radius)
        {
            if (!IsTaken(npc, npc.transform.position, spacing * 0.9f))
            {
                reserved[npc] = npc.transform.position;
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