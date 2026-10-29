using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Events;
using static Unity.U2D.Physics.PhysicsShape;

public class GrabIK : MonoBehaviour
{
    [Header("Rig References")]
    [Tooltip("Rig containing the Hand TwoBoneIKConstraint")]
    [SerializeField] private Rig armRig;
    [Tooltip("Target transform driving the TwoBoneIKConstraint")]
    [SerializeField] private Transform handTarget;
    [Tooltip("Hint transform for the elbow pole vector")]
    [SerializeField] private Transform elbowHint;

    [Header("Bones")]
    [SerializeField] private Transform upperArm;
    [SerializeField] private Transform forearm;
    [SerializeField] public Transform hand;

    [Header("Spine Lean / Aim")]
    [Tooltip("MultiAimConstraint configured on Spine/Chest bones")]
    [SerializeField] private MultiAimConstraint spineAimConstraint;
    [Tooltip("Transform the spine aims toward")]
    [SerializeField] private Transform spineAimTarget;
    [SerializeField, Range(0.5f, 1f)] private float leanStartRatio = 0.75f;
    [SerializeField, Range(0.1f, 1f)] private float maxLeanWeight = 0.85f;

    [Header("Debug")]
    [SerializeField] private bool gizmos;

    private float armLength;
    private bool isGrabbing;
    public bool IsReaching => isGrabbing;
    [SerializeField] private Transform holdPoint;   // same object as HandController.holdPoint

    private void Awake()
    {
        armLength = Vector3.Distance(upperArm.position, forearm.position)
                  + Vector3.Distance(forearm.position, hand.position);

        if (armRig != null) armRig.weight = 0f;
        if (spineAimConstraint != null) spineAimConstraint.weight = 0f;

    }
    

    public IEnumerator ReachRoutine(Transform gripTarget, GrabProfile profile, Action onContact = null)
    {
        if (isGrabbing || gripTarget == null || armRig == null)
        {
            Debug.LogWarning($"Cannot start reach. isGrabbing: {isGrabbing}, target: {gripTarget}");
            yield break;
        }

        isGrabbing = true;

        hand.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        handTarget.SetPositionAndRotation(startPos, startRot);

        Matrix4x4 offset = PoseUtil.Local(profile.handOffsetPosition, profile.handOffsetEuler);
        Matrix4x4 holdInHand = PoseUtil.Pose(hand).inverse * PoseUtil.Pose(holdPoint);
        Matrix4x4 handPose = PoseUtil.Pose(gripTarget) * offset * holdInHand.inverse;

        Vector3 targetPos = handPose.GetPosition();
        Quaternion targetRot = handPose.rotation;

        float targetLean = CalculateLeanWeight(targetPos);
        if (spineAimTarget != null) spineAimTarget.position = targetPos;

        // ================= REACH PHASE =================
        float elapsed = 0f;
        while (elapsed < profile.reachDuration)
        {
            elapsed += Time.deltaTime;
            float t = profile.reachCurve.Evaluate(Mathf.Clamp01(elapsed / profile.reachDuration));

            handTarget.SetPositionAndRotation(
                Vector3.Lerp(startPos, targetPos, t),
                Quaternion.Slerp(startRot, targetRot, t));

            armRig.weight = t;
            if (spineAimConstraint != null)
                spineAimConstraint.weight = Mathf.Lerp(0f, targetLean, t);

            yield return null;
        }

        handTarget.SetPositionAndRotation(targetPos, targetRot);
        armRig.weight = 1f;
        if (spineAimConstraint != null) spineAimConstraint.weight = targetLean;

        // One frame so the IK is applied, then the contact moment
        yield return null;
        onContact?.Invoke();

        // ============ HAND OFF TO THE HOLD ANIMATION ============
        // No return to the start pose anymore. The IK fades out and the
        // animator's hold clip takes over the arm.
        float blendDuration = Mathf.Max(0.01f, profile.ikBlendOutDuration);
        elapsed = 0f;
        while (elapsed < blendDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendDuration));

            armRig.weight = Mathf.Lerp(1f, 0f, t);
            if (spineAimConstraint != null)
                spineAimConstraint.weight = Mathf.Lerp(targetLean, 0f, t);

            yield return null;
        }

        armRig.weight = 0f;
        if (spineAimConstraint != null) spineAimConstraint.weight = 0f;
        handTarget.SetPositionAndRotation(startPos, startRot);
        isGrabbing = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!gizmos || upperArm == null || forearm == null || hand == null) return;

        float length = Vector3.Distance(upperArm.position, forearm.position)
                     + Vector3.Distance(forearm.position, hand.position);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(upperArm.position, length * leanStartRatio);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(upperArm.position, length);
    }

    private float CalculateLeanWeight(Vector3 targetPos)
    {
        float distanceToTarget = Vector3.Distance(upperArm.position, targetPos);
        float comfortableReach = armLength * leanStartRatio;
        if (distanceToTarget <= comfortableReach) return 0f;

        float overReach = distanceToTarget + comfortableReach * -1f;
        float maxOverReach = armLength * Mathf.Lerp(1f, 0f, leanStartRatio) + 0.3f;
        return Mathf.Clamp01(overReach / maxOverReach) * maxLeanWeight;
    }
}