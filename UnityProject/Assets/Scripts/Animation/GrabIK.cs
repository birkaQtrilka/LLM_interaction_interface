using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

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
    [SerializeField] private float armLenghtOffset = -0.1f;
    [Tooltip("Extra distance (meters) to overshoot the lean. Calculates reach as if the arm was shorter, keeping the real arm slightly bent.")]
    [SerializeField, Range(0f, 0.5f)] private float leanOvershoot = 0.1f;

    [Header("Crouch / Knee Bend")]
    [Tooltip("Rig holding the two leg TwoBoneIKConstraints and the hips OverrideTransform")]
    [SerializeField] private Rig crouchRig;
    [Tooltip("OverrideTransform on the hips bone. Space MUST be set to Pivot in the inspector.")]
    [SerializeField] private OverrideTransform hipsOverride;
    [SerializeField] private Transform leftFootTarget;
    [SerializeField] private Transform rightFootTarget;
    [Tooltip("Actual foot bones (used to plant the IK targets where the feet currently are)")]
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;
    [Tooltip("Knee hint transforms. Kept at a fixed pose relative to Hint Anchor.")]
    [SerializeField] private Transform leftKneeHint;
    [SerializeField] private Transform rightKneeHint;
    [Tooltip("Non-animated transform the hints are kept relative to (usually the NPC root). Defaults to this object's root.")]
    [SerializeField] private Transform hintAnchor;
    [Tooltip("Crouching starts once the spine lean is maxed out. Positive = crouch later (stretch more), negative = crouch earlier.")]
    [SerializeField] private float crouchReachMargin = 0f;
    [Tooltip("Maximum hips drop in meters")]
    [SerializeField] private float maxCrouchDepth = 0.6f;
    [Tooltip("Ignore tiny drops so we don't crouch for a few cm")]
    [SerializeField] private float minCrouchDepth = 0.05f;
    [Tooltip("Extra reach (m) the full spine lean adds. Raise it if the spine bends further than this in practice, so the crouch starts later.")]
    [SerializeField] private float leanReachBonus = 0.3f;

    [Header("Debug")]
    [SerializeField] private bool gizmos;

    private float armLength;
    private bool isGrabbing;
    public bool IsReaching => isGrabbing;
    [SerializeField] private Transform holdPoint;   // same object as HandController.holdPoint

    private Transform hips;

    // Hints are stored relative to the anchor so nothing can drag them around
    private Vector3 leftHintLocal, rightHintLocal;
    private bool hintsCaptured;

    // Feet are pinned in world space while crouching
    private bool feetPlanted;
    private Vector3 leftPlantPos, rightPlantPos;
    private Quaternion leftPlantRot, rightPlantRot;

    private void Awake()
    {
        armLength = Vector3.Distance(upperArm.position, forearm.position)
                  + Vector3.Distance(forearm.position, hand.position) + armLenghtOffset;

        if (armRig != null) armRig.weight = 0f;
        if (spineAimConstraint != null) spineAimConstraint.weight = 0f;

        if (hipsOverride != null) hips = hipsOverride.data.constrainedObject;

        // Capture hint poses now, while the character is in its standing setup pose
        if (hintAnchor == null) hintAnchor = transform.root;
        if (leftKneeHint != null) leftHintLocal = hintAnchor.InverseTransformPoint(leftKneeHint.position);
        if (rightKneeHint != null) rightHintLocal = hintAnchor.InverseTransformPoint(rightKneeHint.position);
        hintsCaptured = true;

        SetCrouch(0f, 0f);
    }

    // Runs before the animator/rig evaluates this frame
    private void Update()
    {
        if (hintsCaptured)
        {
            if (leftKneeHint != null) leftKneeHint.position = hintAnchor.TransformPoint(leftHintLocal);
            if (rightKneeHint != null) rightKneeHint.position = hintAnchor.TransformPoint(rightHintLocal);
        }

        if (feetPlanted)
        {
            if (leftFootTarget != null) leftFootTarget.SetPositionAndRotation(leftPlantPos, leftPlantRot);
            if (rightFootTarget != null) rightFootTarget.SetPositionAndRotation(rightPlantPos, rightPlantRot);
        }
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

        // Lean covers slightly-out-of-reach, crouch only takes over once lean is maxed
        float crouchDepth = CalculateCrouchDepth(targetPos);
        float targetLean = CalculateLeanWeight(targetPos, crouchDepth);
        if (gizmos)
            Debug.Log($"Reach: below={-(targetPos.y - upperArm.position.y):F2} " +
                      $"horiz={new Vector2(targetPos.x - upperArm.position.x, targetPos.z - upperArm.position.z).magnitude:F2} " +
                      $"crouch={crouchDepth:F2} lean={targetLean:F2} maxReach={FullLeanDistance:F2}");
        if (spineAimTarget != null) spineAimTarget.position = targetPos;

        if (crouchDepth > 0f) PlantFeet();

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
            SetCrouch(crouchDepth, t);
            if (spineAimConstraint != null)
                spineAimConstraint.weight = Mathf.Lerp(0f, targetLean, t);

            yield return null;
        }

        handTarget.SetPositionAndRotation(targetPos, targetRot);
        armRig.weight = 1f;
        SetCrouch(crouchDepth, 1f);
        if (spineAimConstraint != null) spineAimConstraint.weight = targetLean;

        // One frame so the IK is applied, then the contact moment
        yield return null;
        onContact?.Invoke();

        // ============ HAND OFF TO THE HOLD ANIMATION ============
        float blendDuration = Mathf.Max(0.01f, profile.ikBlendOutDuration);
        elapsed = 0f;
        while (elapsed < blendDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendDuration));

            armRig.weight = Mathf.Lerp(1f, 0f, t);
            SetCrouch(crouchDepth, 1f - t);   // stand back up
            if (spineAimConstraint != null)
                spineAimConstraint.weight = Mathf.Lerp(targetLean, 0f, t);

            yield return null;
        }

        armRig.weight = 0f;
        SetCrouch(0f, 0f);
        feetPlanted = false;
        if (spineAimConstraint != null) spineAimConstraint.weight = 0f;
        handTarget.SetPositionAndRotation(startPos, startRot);
        isGrabbing = false;
    }

    // ------------------------------------------------------------------
    // Reach distances
    // ------------------------------------------------------------------

    /// <summary>The arm length used for calculation minus overshoot to force the body closer to the target.</summary>
    private float EffectiveArmLength => Mathf.Max(0.01f, armLength - leanOvershoot);

    /// <summary>How far past the comfortable reach the lean goes from 0 to full weight.</summary>
    private float LeanMaxOverReach => EffectiveArmLength * (1f - leanStartRatio) + leanReachBonus;

    /// <summary>Shoulder-to-target distance at which the lean reaches full weight.
    /// Beyond this, the crouch takes over.</summary>
    private float FullLeanDistance => EffectiveArmLength * leanStartRatio + LeanMaxOverReach + crouchReachMargin;

    // ------------------------------------------------------------------
    // Crouch
    // ------------------------------------------------------------------

    /// <summary>
    /// How far the hips need to drop so that the shoulder ends up within
    /// FullLeanDistance of the target. Returns 0 when the arm + lean can already reach it.
    /// </summary>
    private float CalculateCrouchDepth(Vector3 targetPos)
    {
        if (crouchRig == null || hipsOverride == null) return 0f;

        Vector3 delta = targetPos - upperArm.position;
        float below = -delta.y;
        if (below <= 0f) return 0f;                   // at or above shoulder height: never crouch

        float horizontal = new Vector2(delta.x, delta.z).magnitude;
        float maxReach = FullLeanDistance;

        // Dropping the hips only reduces the vertical gap. If the target is already
        // out of reach horizontally, crouching can't fix it, so don't crouch at all
        // (the hand just stretches, and the NPC should walk closer).
        if (horizontal >= maxReach) return 0f;

        // Smallest drop d such that (below - d)² + horizontal² <= maxReach²
        float allowedBelow = Mathf.Sqrt(maxReach * maxReach - horizontal * horizontal);
        float needed = below - allowedBelow;

        if (needed < minCrouchDepth) return 0f;
        return Mathf.Min(needed, maxCrouchDepth);
    }
    /// <summary>Pins the foot IK targets to where the feet are right now.</summary>
    private void PlantFeet()
    {
        if (leftFoot != null && leftFootTarget != null)
        {
            leftFoot.GetPositionAndRotation(out leftPlantPos, out leftPlantRot);
            leftFootTarget.SetPositionAndRotation(leftPlantPos, leftPlantRot);
        }
        if (rightFoot != null && rightFootTarget != null)
        {
            rightFoot.GetPositionAndRotation(out rightPlantPos, out rightPlantRot);
            rightFootTarget.SetPositionAndRotation(rightPlantPos, rightPlantRot);
        }
        feetPlanted = true;
    }

    /// <summary>depth in meters, amount 0..1 (blend)</summary>
    private void SetCrouch(float depth, float amount)
    {
        if (crouchRig == null) return;

        amount = Mathf.Clamp01(amount);

        // Feet stay pinned at full weight for the whole crouch. Only the hips drop is blended.
        crouchRig.weight = depth > 0f ? 1f : 0f;

        if (hipsOverride == null || hips == null) return;

        // OverrideTransform in Pivot space offsets along the bone's local axes,
        // so convert "world down" into hips-local space.
        Vector3 localDown = hips.InverseTransformDirection(Vector3.down);

        var data = hipsOverride.data;
        data.position = localDown * (depth * amount);
        hipsOverride.data = data;
    }

    // ------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        if (!gizmos || upperArm == null || forearm == null || hand == null) return;

        // Calculate arm length identically to runtime (including offset) so gizmos perfectly match
        float currentArmLength = Application.isPlaying && armLength > 0f ? armLength :
            (Vector3.Distance(upperArm.position, forearm.position) + Vector3.Distance(forearm.position, hand.position) + armLenghtOffset);

        float effectiveLength = Mathf.Max(0.01f, currentArmLength - leanOvershoot);
        float leanMaxOver = effectiveLength * (1f - leanStartRatio) + leanReachBonus;
        float fullLeanDist = effectiveLength * leanStartRatio + leanMaxOver + crouchReachMargin;

        Gizmos.color = Color.cyan;      // lean starts
        Gizmos.DrawWireSphere(upperArm.position, effectiveLength * leanStartRatio);
        Gizmos.color = Color.red;       // effective arm length boundary
        Gizmos.DrawWireSphere(upperArm.position, effectiveLength);
        Gizmos.color = Color.yellow;    // crouch starts (lean maxed)
        Gizmos.DrawWireSphere(upperArm.position, fullLeanDist);
    }

    private float CalculateLeanWeight(Vector3 targetPos, float crouchDepth = 0f)
    {
        // Shoulder position after the crouch has lowered it
        Vector3 shoulder = upperArm.position + Vector3.down * crouchDepth;

        float distanceToTarget = Vector3.Distance(shoulder, targetPos);

        // Base comfortable reach off of the shorter "effective" arm length
        float comfortableReach = EffectiveArmLength * leanStartRatio;
        if (distanceToTarget <= comfortableReach) return 0f;

        float overReach = distanceToTarget - comfortableReach;
        return Mathf.Clamp01(overReach / LeanMaxOverReach) * maxLeanWeight;
    }
}