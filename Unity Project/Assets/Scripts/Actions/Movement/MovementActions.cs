using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

static class MovementActions
{
    // Backwards compatibility alias
    public static Dictionary<NPC, Vector3> Reserved => MovementReservation.Reserved;

    static readonly int HorizontalHash = Animator.StringToHash("Horizontal");
    static readonly int VerticalHash = Animator.StringToHash("Vertical");
    static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    static readonly int TurnAngleHash = Animator.StringToHash("TurnAngle");
    static readonly int DoTurnHash = Animator.StringToHash("DoTurn");
    static readonly int MoveHash = Animator.StringToHash("Move");
    static readonly int TurnInPlaceHash = Animator.StringToHash("TurnInPlace");

    public static AnimAction Build(NPC agent, TransOrPos pos, ActionData action)
    {
        var lookHandler = new LookAtHandler(agent, pos);
        Coroutine walkRoutine = null;

        void start()
        {
            float standoff = agent.Nav.radius + 0.5f;
            if (pos.IsTransform && pos.transform != null && pos.transform.TryGetComponent(out NavMeshAgent other))
                standoff += other.radius;

            float startRadius = pos.IsTransform ? standoff : 0f;

            Vector3 destination;
            if (MovementReservation.TryReserve(agent, pos.position, out Vector3 spot, agent.Nav.radius + 0.5f, startRadius))
                destination = spot;
            else if (pos.IsTransform)
                destination = MovementReservation.ApproachPoint(agent, pos.transform);
            else
                destination = pos.position;

            if (Vector3.Distance(agent.transform.position, destination) > agent.Nav.stoppingDistance + 0.05f)
                agent.Nav.SetDestination(destination);
        }

        IEnumerator behavior()
        {
            // Wait for NavMesh to finish asynchronous path calculation
            yield return null;
            while (agent.Nav.pathPending)
                yield return null;


            // If already in range or no path needed, turn to face the target
            if (!agent.Nav.hasPath || agent.Nav.remainingDistance <= agent.Nav.stoppingDistance + 0.05f)
            {
                yield return agent.StartCoroutine(TurnTowards(agent, pos.position));
            }
            else
            {
                walkRoutine = agent.StartCoroutine(WalkAndFace(agent, pos.position));
                lookHandler.StartWatching();

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
            MovementReservation.Release(agent);
            lookHandler.StopWatching();

            if (walkRoutine != null)
            {
                agent.StopCoroutine(walkRoutine);
                walkRoutine = null;
            }

            agent.Anim.SetFloat(HorizontalHash, 0f);
            agent.Anim.SetFloat(VerticalHash, 0f);
            agent.Anim.SetFloat(MotionSpeedHash, 1f);

            agent.Nav.ResetPath();
            agent.Nav.isStopped = false;
        }

        return new AnimAction(action, start, behavior(), end);
    }

    /// <summary>
    /// Turns the agent towards a target position in world space unconditionally.
    /// </summary>
    public static IEnumerator TurnTowards(NPC agent, Vector3 targetPosition)
    {
        Vector3 dir = targetPosition - agent.transform.position;
        return TurnTowardsDirection(agent, dir);
    }

    /// <summary>
    /// Turns the agent towards a direction vector in world space unconditionally.
    /// </summary>
    public static IEnumerator TurnTowardsDirection(NPC agent, Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) yield break;

        direction.Normalize();
        float turnAngle = Vector3.SignedAngle(agent.transform.forward, direction, Vector3.up);

        NavMeshAgent nav = agent.Nav;
        nav.isStopped = true;
        agent.Anim.SetFloat(TurnAngleHash, turnAngle);
        agent.Anim.SetTrigger(DoTurnHash);

        Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
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

    static IEnumerator WalkAndFace(NPC agent, Vector3 pos)
    {
        var nav = agent.Nav;

        Vector3 initialDir = nav.steeringTarget - agent.transform.position;
        initialDir.y = 0f;

        // Only turn in place if the required turn exceeds MaxTurnAngle
        if (initialDir.sqrMagnitude > 0.001f)
        {
            float turnAngle = Vector3.SignedAngle(agent.transform.forward, initialDir.normalized, Vector3.up);
            if (Mathf.Abs(turnAngle) > agent.MaxTurnAngle)
                yield return TurnTowardsDirection(agent, initialDir);
        }

        while (nav.remainingDistance > nav.stoppingDistance + 0.05f)
        {
            Vector3 targetDir = nav.remainingDistance < agent.FaceDistance
                ? pos - agent.transform.position
                : nav.desiredVelocity;
            targetDir.y = 0f;

            float h = 0f;
            float v = 0f;

            if (targetDir.sqrMagnitude > 0.001f)
            {
                Vector3 normTarget = targetDir.normalized;
                float angle = Vector3.SignedAngle(agent.transform.forward, normTarget, Vector3.up);
                h = Mathf.Clamp(angle / agent.MaxTurnAngle, -1f, 1f);
                v = Mathf.Max(0f, Vector3.Dot(agent.transform.forward, normTarget));
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
}