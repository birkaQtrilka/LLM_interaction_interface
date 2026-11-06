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
    [Tooltip("Spine bones that share the lean, ordered lowest to highest. Each needs its own MultiAimConstraint and aim target. Constraints must also be ordered lowest to highest in the rig hierarchy.")]
    [SerializeField] private SpineLeanLink[] spineChain;
    [SerializeField, Range(0.5f, 1f)] private float leanStartRatio = 0.75f;
    [SerializeField, Range(0.1f, 1f)] private float maxLeanWeight = 0.85f;
    [SerializeField] private float armLenghtOffset = -0.1f;
    [Tooltip("Extra distance (meters) to overshoot the lean. Calculates reach as if the arm was shorter, keeping the real arm slightly bent.")]
    [SerializeField, Range(0f, 0.5f)] private float leanOvershoot = 0.1f;
    [Tooltip("Additional downward aim pitch for low reaches, without moving the hand target (degrees).")]
    [SerializeField, Range(0f, 45f)] private float extraDownwardLeanPitch = 20f;
    [Tooltip("Maximum change from the unleaned spine aim direction (degrees).")]
    [SerializeField, Range(0f, 85f)] private float maxForwardLeanAngle = 75f;
    [Tooltip("Fraction of reach time reserved for leaning before knees may bend.")]
    [SerializeField, Range(0.1f, 0.9f)] private float leanFirstFraction = 0.55f;
    [Tooltip("Shoulder motion tolerance between evaluated frames before permitting crouch (meters).")]
    [SerializeField, Min(0.001f)] private float leanSettleTolerance = 0.02f;

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
    [Tooltip("Distance beyond comfortable arm reach over which lean ramps to full weight; not an assumed reach bonus.")]
    [SerializeField] private float leanReachBonus = 0.3f;
    [Tooltip("Maximum hips movement speed (meters per second).")]
    [SerializeField, Min(0.1f)] private float crouchResponseSpeed = 2.5f;
    [Tooltip("Ignore residual changes smaller than this distance (meters).")]
    [SerializeField, Min(0.001f)] private float reachTolerance = 0.015f;
    [Tooltip("Maximum additional settling time before contact (seconds), even for unreachable targets.")]
    [SerializeField, Range(0f, 0.5f)] private float maxReachSettleTime = 0.2f;

    [Header("Shoulder Twist")]
    [Tooltip("Multi-Aim Constraint on a chest bone ABOVE the lean bones. Only the spine axis should be constrained.")]
    [SerializeField] private MultiAimConstraint chestTwistConstraint;
    [SerializeField] private Transform chestTwistTarget;
    [Tooltip("Twist weight at full lean/crouch. Keep low for a gentle turn.")]
    [SerializeField, Range(0f, 1f)] private float maxTwistWeight = 0.5f;
    [Tooltip("How quickly the twist weight follows lean/crouch (weight per second).")]
    [SerializeField, Min(0.1f)] private float twistResponseSpeed = 2f;
    private float twistWeight;

    private Vector3 sampledShoulder, sampledLeanShoulder, previousLeanShoulder;
    private float sampledLeanWeight;
    private int sampleFrame = -1, stableLeanFrames;
    private bool resolvingPosture, crouchAuthorized;
    private float requestedLeanWeight, appliedCrouchDepth;
    private Vector3 postureTarget;
    private Quaternion reachStartRotation;
    private Vector3 reachStartPosition;

    // Global lean weight shared by every link in the spine chain (each link scales it by its weightScale)
    private float leanWeight;

    public float AppliedCrouchDepth => appliedCrouchDepth;
    public float LeanWeight => leanWeight;
    public float RequestedLeanWeight => requestedLeanWeight;
    public bool CrouchAuthorized => crouchAuthorized;
    public float ContactError { get; private set; }
    public float SpineLeanAngle
    {
        get
        {
            var top = TopLink;
            return top != null && top.neutralAim.sqrMagnitude > 0.001f
                ? Vector3.Angle(top.neutralAim, top.Bone.TransformDirection(top.axis)) : 0f;
        }
    }

    [Header("Debug")]
    [SerializeField] private bool gizmos;
    [SerializeField] Vector3 handForward;
    [SerializeField] Vector3 handUp;
    private float armLength;
    private bool isGrabbing;
    public bool IsReaching => isGrabbing;
    [SerializeField] private Transform holdPoint;   // same object as HandController.holdPoint

    private Transform hips;

    // Hints are stored relative to the anchor so nothing can drag them around
    private Vector3 leftHintLocal, rightHintLocal;
    private Vector3 elbowOffsetFromShoulder;
    private bool hintsCaptured;

    // Feet are pinned in world space while crouching
    private bool feetPlanted;
    private Vector3 leftPlantPos, rightPlantPos;
    private Quaternion leftPlantRot, rightPlantRot;

    private Animator reachAnimator;
    private AnimatorCullingMode previousCullingMode;
    private int reachVersion;

    private void Awake()
    {
        reachAnimator = GetComponent<Animator>();
        armLength = upperArm != null && forearm != null && hand != null
            ? Vector3.Distance(upperArm.position, forearm.position)
                + Vector3.Distance(forearm.position, hand.position) + armLenghtOffset : 0f;

        if (armRig != null) armRig.weight = 0f;
        SetLeanWeight(0f);
        if (chestTwistConstraint != null) chestTwistConstraint.weight = 0f;

        if (hipsOverride != null) hips = hipsOverride.data.constrainedObject;

        // Capture hint poses now, while the character is in its standing setup pose
        if (hintAnchor == null) hintAnchor = transform.root;
        if (leftKneeHint != null) leftHintLocal = hintAnchor.InverseTransformPoint(leftKneeHint.position);
        if (rightKneeHint != null) rightHintLocal = hintAnchor.InverseTransformPoint(rightKneeHint.position);

        if (elbowHint != null && upperArm != null)
        {
            // By storing the offset as a Direction instead of a Point, the elbow hint will track 
            // the shoulder's position without spinning wildly when the arm bone rotates.
            elbowOffsetFromShoulder = hintAnchor.InverseTransformDirection(elbowHint.position - upperArm.position);
        }

        hintsCaptured = true;
        SetCrouch(0f, 0f);
    }

    public void SetTargetPose(Vector3 position, Quaternion rotation)
    {
        if (handTarget != null) handTarget.SetPositionAndRotation(position, rotation);
    }
    public Transform UpperArm => upperArm;

    // Runs before the animator/rig evaluates this frame
    private void Update()
    {
        if (hintsCaptured)
        {
            if (leftKneeHint != null) leftKneeHint.position = hintAnchor.TransformPoint(leftHintLocal);
            if (rightKneeHint != null) rightKneeHint.position = hintAnchor.TransformPoint(rightHintLocal);

            // Make the elbow hint perfectly follow the shoulder's world location (crouch drops and lean offsets)
            if (elbowHint != null && upperArm != null)
            {
                elbowHint.position = upperArm.position + hintAnchor.TransformDirection(elbowOffsetFromShoulder);
            }
        }

        if (feetPlanted)
        {
            if (leftFootTarget != null) leftFootTarget.SetPositionAndRotation(leftPlantPos, leftPlantRot);
            if (rightFootTarget != null) rightFootTarget.SetPositionAndRotation(rightPlantPos, rightPlantRot);
        }
    }

    private void LateUpdate()
    {
        if (!isGrabbing || upperArm == null) return;
        sampledShoulder = upperArm.position;
        Vector3 crouchOffset = Vector3.zero;
        if (hipsOverride != null && hips != null && crouchRig != null)
            crouchOffset = crouchRig.weight * hipsOverride.data.positionWeight * hipsOverride.weight * hips.TransformDirection(hipsOverride.data.position);
        sampledLeanShoulder = sampledShoulder - crouchOffset;
        sampledLeanWeight = HasSpineLean ? leanWeight : 0f;
        bool leanEvaluated = !HasSpineLean || sampledLeanWeight >= requestedLeanWeight - 0.001f;
        stableLeanFrames = leanEvaluated && sampleFrame >= 0
            && Vector3.Distance(sampledLeanShoulder, previousLeanShoulder) <= leanSettleTolerance
            ? stableLeanFrames + 1 : 0;
        previousLeanShoulder = sampledLeanShoulder;
        sampleFrame = Time.frameCount;
        UpdateSpineAim();
    }

    private bool extraLeanRequired;

    private void UpdateReachPosture(float progress)
    {
        float leanProgress = Mathf.Clamp01(progress / Mathf.Clamp(leanFirstFraction, 0.1f, 0.9f));
        bool evaluated = sampleFrame >= 0 && sampleFrame < Time.frameCount && stableLeanFrames >= 2;
        if (!crouchAuthorized && HasSpineLean && leanProgress >= 1f && evaluated
            && Vector3.Distance(sampledShoulder, postureTarget) > EffectiveArmLength + reachTolerance)
            extraLeanRequired = true;
        if (extraLeanRequired)
            requestedLeanWeight = Mathf.MoveTowards(requestedLeanWeight, Mathf.Clamp01(maxLeanWeight),
                Time.deltaTime / Mathf.Max(0.1f, leanFirstFraction));
        if (HasSpineLean)
            SetLeanWeight(Mathf.SmoothStep(0f, requestedLeanWeight, leanProgress));
        bool leanExhausted = !extraLeanRequired || sampledLeanWeight >= Mathf.Clamp01(maxLeanWeight) - 0.001f;
        if (!crouchAuthorized && (!HasSpineLean || leanProgress >= 1f) && evaluated && leanExhausted)
            crouchAuthorized = true;
        if (!crouchAuthorized) return;
        float desiredDepth = CalculateCrouchDepth(postureTarget);
        if (Mathf.Abs(desiredDepth - appliedCrouchDepth) > reachTolerance)
            appliedCrouchDepth = Mathf.MoveTowards(appliedCrouchDepth, desiredDepth,
                Mathf.Max(0.1f, crouchResponseSpeed) * Time.deltaTime);
        if (appliedCrouchDepth > 0f && !feetPlanted) PlantFeet();
        SetCrouch(appliedCrouchDepth, 1f);
    }

    public IEnumerator ReachRoutine(Transform gripTarget, GrabProfile profile, Action onContact = null)
    {
        if (isGrabbing || !isActiveAndEnabled || gripTarget == null || profile == null || armRig == null
            || handTarget == null || upperArm == null || forearm == null || hand == null || holdPoint == null)
        {
            Debug.LogWarning($"Cannot start reach. isGrabbing: {isGrabbing}, target: {gripTarget}");
            yield break;
        }

        isGrabbing = true;
        int version = ++reachVersion;
        hand.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        reachStartPosition = startPos;
        reachStartRotation = startRot;
        if (reachAnimator != null)
        {
            previousCullingMode = reachAnimator.cullingMode;
            reachAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        try
        {
            handTarget.SetPositionAndRotation(startPos, startRot);

            Matrix4x4 offset = PoseUtil.Local(profile.handOffsetPosition, profile.handOffsetEuler);
            Matrix4x4 holdInHand = PoseUtil.Pose(hand).inverse * PoseUtil.Pose(holdPoint);

            Matrix4x4 desiredHoldPose = PoseUtil.Pose(gripTarget) * offset;
            Vector3 desiredHoldPos = desiredHoldPose.GetPosition();

            Vector3 startDir = (startPos - upperArm.position).normalized;
            Vector3 targetDir = (desiredHoldPos - upperArm.position);
            targetDir = targetDir.sqrMagnitude > 0.001f ? targetDir.normalized : Vector3.forward;

            Quaternion armSwing = Quaternion.FromToRotation(startDir, targetDir);
            Quaternion targetRot = armSwing * startRot;

            // We find the horizontal right axis and pitch the hand up slightly (e.g. 20 degrees)
            Vector3 rightAxis = Vector3.Cross(Vector3.up, targetDir).normalized;
            if (rightAxis.sqrMagnitude > 0.001f)
            {
                // A negative angle around the right axis pitches the hand upwards in Unity
                targetRot = Quaternion.AngleAxis(-20f, rightAxis) * targetRot;
            }

            Vector3 localHoldPos = holdInHand.GetPosition();
            Vector3 targetPos = desiredHoldPos - (targetRot * localHoldPos);

            // Lock lean demand before crouching. The hand destination remains the true grip pose.
            postureTarget = targetPos;
            requestedLeanWeight = CalculateLeanWeight(targetPos);
            PrepareSpineAim(targetPos);
            resolvingPosture = true;
            appliedCrouchDepth = 0f;
            crouchAuthorized = false;
            stableLeanFrames = 0;
            sampleFrame = -1;
            float duration = Mathf.Max(0.01f, profile.reachDuration);
            float elapsed = 0f;
            int finalPoseFrames = 0;
            while (elapsed < duration + Mathf.Clamp(maxReachSettleTime, 0f, 0.5f))
            {
                if (version != reachVersion || !isGrabbing || !isActiveAndEnabled || gripTarget == null) yield break;
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float t = progress >= 1f ? 1f : Mathf.Clamp01(profile.reachCurve.Evaluate(progress));
                handTarget.SetPositionAndRotation(Vector3.Lerp(startPos, targetPos, t),
                    Quaternion.Slerp(startRot, targetRot, t));
                armRig.weight = t;
                UpdateReachPosture(progress);
                UpdateTwist();
                if (progress >= 1f && sampleFrame >= 0
                    && Vector3.Distance(hand.position, targetPos) <= reachTolerance)
                    finalPoseFrames++;
                else finalPoseFrames = 0;
                yield return null;
                if (finalPoseFrames >= 2) break;
            }
            resolvingPosture = false;
            if (version != reachVersion || !isGrabbing || !isActiveAndEnabled || gripTarget == null)
                yield break;
            handTarget.SetPositionAndRotation(targetPos, targetRot);
            armRig.weight = 1f;
            yield return null;
            if (version != reachVersion || !isGrabbing || !isActiveAndEnabled || gripTarget == null)
                yield break;
            ContactError = Vector3.Distance(hand.position, targetPos);
            if (gizmos)
                Debug.Log($"Reach: lean={requestedLeanWeight:F2} angle={SpineLeanAngle:F1} " +
                          $"crouch={appliedCrouchDepth:F2} contactError={ContactError:F3}", this);
            onContact?.Invoke();

            // Blend out the depth actually applied, not an estimated standing-pose deficit.
            float crouchDepth = appliedCrouchDepth;
            float targetLean = requestedLeanWeight;
            float blendDuration = Mathf.Max(0.01f, profile.ikBlendOutDuration);
            float blendElapsed = 0f;
            while (blendElapsed < blendDuration)
            {
                if (version != reachVersion || !isGrabbing || !isActiveAndEnabled) yield break;
                blendElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendElapsed / blendDuration));
                armRig.weight = 1f - t;
                appliedCrouchDepth = crouchDepth * (1f - t);
                SetCrouch(appliedCrouchDepth, 1f);
                SetLeanWeight(Mathf.Lerp(targetLean, 0f, t));
                UpdateTwist();
                yield return null;
            }
        }
        finally
        {
            if (version == reachVersion) ResetReach();
        }
    }

    private void OnDisable()
    {
        if (isGrabbing) ResetReach();
    }

    private void ResetReach()
    {
        if (reachAnimator != null && isGrabbing) reachAnimator.cullingMode = previousCullingMode;
        reachVersion++;
        SetCrouch(0f, 0f);
        feetPlanted = false;
        resolvingPosture = false;
        crouchAuthorized = false;
        extraLeanRequired = false;
        sampleFrame = -1;
        stableLeanFrames = 0;
        appliedCrouchDepth = 0f;
        requestedLeanWeight = 0f;
        spineAimDirection = Vector3.zero;
        ResetTwist();
        SetLeanWeight(0f);
        if (armRig != null) armRig.weight = 0f;
        if (handTarget != null && isGrabbing)
            handTarget.SetPositionAndRotation(reachStartPosition, reachStartRotation);
        isGrabbing = false;
    }

    public IEnumerator ArmReachPhase(GrabProfile profile, Pos startPos, Pos targetPos, Action<float> onUpdate = null)
    {
        float elapsed = 0f;
        while (elapsed < profile.reachDuration)
        {
            elapsed += Time.deltaTime;
            float t = profile.reachCurve.Evaluate(Mathf.Clamp01(elapsed / profile.reachDuration));

            handTarget.SetPositionAndRotation(
                Vector3.Lerp(startPos.p, targetPos.p, t),
                Quaternion.Slerp(startPos.r, targetPos.r, t));

            armRig.weight = t;
            onUpdate?.Invoke(t);

            yield return null;
        }
        armRig.weight = 1f;
    }

    public IEnumerator ArmDownPhase(GrabProfile profile, Action<float> onUpdate = null)
    {
        float blendDuration = Mathf.Max(0.01f, profile.ikBlendOutDuration);
        float elapsed = 0f;
        while (elapsed < blendDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendDuration));
            armRig.weight = Mathf.Lerp(1f, 0f, t);
            onUpdate?.Invoke(t);
            yield return null;
        }

        armRig.weight = 0f;
    }

    // ------------------------------------------------------------------
    // Reach distances
    // ------------------------------------------------------------------

    private float EffectiveArmLength => Mathf.Max(0.01f, armLength - leanOvershoot);
    private float LeanMaxOverReach => EffectiveArmLength * (1f - leanStartRatio) + leanReachBonus;
    private float FullLeanDistance => EffectiveArmLength * leanStartRatio + LeanMaxOverReach + crouchReachMargin;

    // ------------------------------------------------------------------
    // Crouch
    // ------------------------------------------------------------------

    private float CalculateCrouchDepth(Vector3 targetPos)
    {
        if (crouchRig == null || hipsOverride == null || hips == null || sampleFrame < 0) return 0f;
        float reach = Mathf.Max(0.01f, EffectiveArmLength + crouchReachMargin);
        float needed = ResidualCrouchDepth(sampledLeanShoulder, targetPos, reach);
        if (needed < Mathf.Max(0f, minCrouchDepth)) return 0f;
        return Mathf.Min(needed, Mathf.Max(0f, maxCrouchDepth));
    }

    // A vertical pelvis drop cannot fix a horizontal shortfall. Return no fake solution.
    public static float ResidualCrouchDepth(Vector3 leanShoulder, Vector3 targetPos, float reach)
    {
        Vector3 delta = targetPos - leanShoulder;
        double horizontalSquared = (double)delta.x * delta.x + (double)delta.z * delta.z;
        double reachSquared = (double)reach * reach;
        if (double.IsNaN(horizontalSquared) || double.IsInfinity(horizontalSquared)
            || float.IsNaN(delta.y) || float.IsInfinity(delta.y)
            || float.IsNaN(reach) || float.IsInfinity(reach) || reach <= 0f
            || delta.y >= 0f || horizontalSquared > reachSquared) return 0f;
        float allowedBelow = (float)Math.Sqrt(Math.Max(0d, reachSquared - horizontalSquared));
        return Mathf.Max(0f, -delta.y - allowedBelow);
    }

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

    private void SetCrouch(float depth, float amount)
    {
        if (crouchRig == null) return;

        amount = Mathf.Clamp01(amount);
        crouchRig.weight = depth > 0f ? 1f : 0f;

        if (hipsOverride == null || hips == null) return;

        Vector3 localDown = hips.InverseTransformDirection(Vector3.down);
        var data = hipsOverride.data;
        data.position = localDown * (depth * amount);
        hipsOverride.data = data;
    }

    private bool HasTwist => chestTwistConstraint != null && chestTwistTarget != null
    && chestTwistConstraint.data.constrainedObject != null;

    private void UpdateTwist()
    {
        if (!HasTwist) return;
        float lean = maxLeanWeight > 0.001f ? LeanWeight / maxLeanWeight : 0f;
        float crouch = maxCrouchDepth > 0.001f ? appliedCrouchDepth / maxCrouchDepth : 0f;
        float demand = Mathf.Clamp01(Mathf.Max(lean, crouch)) * maxTwistWeight;
        twistWeight = Mathf.MoveTowards(twistWeight, demand, twistResponseSpeed * Time.deltaTime);
        chestTwistConstraint.weight = twistWeight;
    }

    private void ResetTwist()
    {
        twistWeight = 0f;
        if (chestTwistConstraint != null) chestTwistConstraint.weight = 0f;
    }


    public Quaternion GetHandRotationFacing(Vector3 targetForward, Vector3 targetUp)
    {
        // Orientation where Z is forward and Y is up
        Quaternion worldAim = Quaternion.LookRotation(targetForward.normalized, targetUp.normalized);

        // Orientation where handForwardAxis is forward and handUpAxis is up in local space
        Quaternion localHandBasis = Quaternion.LookRotation(handForward, handUp);

        // Cancel out the bone's local axis mismatch
        return worldAim * Quaternion.Inverse(localHandBasis);
    }

    // ------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        if (!gizmos || upperArm == null || forearm == null || hand == null) return;

        float currentArmLength = Application.isPlaying && armLength > 0f ? armLength :
            (Vector3.Distance(upperArm.position, forearm.position) + Vector3.Distance(forearm.position, hand.position) + armLenghtOffset);

        float effectiveLength = Mathf.Max(0.01f, currentArmLength - leanOvershoot);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(upperArm.position, effectiveLength * leanStartRatio);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(upperArm.position, effectiveLength);
        if (handTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(upperArm.position, handTarget.position);
            Gizmos.DrawWireSphere(handTarget.position, 0.025f);
        }
        if (spineChain != null && HasSpineLean)
        {
            Gizmos.color = Color.green;
            foreach (var link in spineChain)
            {
                if (link == null || !link.IsValid) continue;
                Gizmos.DrawLine(link.Bone.position, link.aimTarget.position);
                Gizmos.DrawWireSphere(link.aimTarget.position, 0.025f);
            }
        }
        if (Application.isPlaying && isGrabbing)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(sampledLeanShoulder, sampledShoulder);
        }
    }

    private float CalculateLeanWeight(Vector3 targetPos, float crouchDepth = 0f)
    {
        if (!HasSpineLean) return 0f;
        // Demand is always computed from the standing shoulder, never reduced by crouch.
        float comfortableReach = EffectiveArmLength * Mathf.Clamp01(leanStartRatio);
        float overReach = Vector3.Distance(upperArm.position, targetPos) - comfortableReach;
        return Mathf.Clamp01(overReach / Mathf.Max(0.01f, LeanMaxOverReach)) * Mathf.Clamp01(maxLeanWeight);
    }

    // ------------------------------------------------------------------
    // Spine lean (multi-vertebra)
    // ------------------------------------------------------------------

    private Vector3 spineAimDirection;

    /// <summary>Lowest valid vertebra in the chain.</summary>
    private SpineLeanLink BaseLink
    {
        get
        {
            if (spineChain == null) return null;
            for (int i = 0; i < spineChain.Length; i++)
                if (spineChain[i] != null && spineChain[i].IsValid) return spineChain[i];
            return null;
        }
    }

    /// <summary>Highest valid vertebra in the chain.</summary>
    private SpineLeanLink TopLink
    {
        get
        {
            if (spineChain == null) return null;
            for (int i = spineChain.Length - 1; i >= 0; i--)
                if (spineChain[i] != null && spineChain[i].IsValid) return spineChain[i];
            return null;
        }
    }

    private bool HasSpineLean => BaseLink != null;

    /// <summary>
    /// Sets the shared lean weight and distributes it to every vertebra constraint,
    /// scaled by each link's weightScale.
    /// </summary>
    private void SetLeanWeight(float weight)
    {
        leanWeight = Mathf.Clamp01(weight);
        if (spineChain == null) return;
        foreach (var link in spineChain)
            if (link != null && link.constraint != null)
                link.constraint.weight = leanWeight * link.weightScale;
    }

    private void PrepareSpineAim(Vector3 targetPos)
    {
        var reference = TopLink;
        if (reference == null) return;

        // Capture each bone's unleaned aim axis (weights are 0 here, so this is the animated pose).
        foreach (var link in spineChain)
        {
            if (link == null || !link.IsValid) continue;
            link.axis = ((IMultiAimConstraintData)link.constraint.data).aimAxis;
            link.neutralAim = link.Bone.TransformDirection(link.axis).normalized;
        }

        // Direction is computed from the top vertebra, which ends up fully aimed along it.
        Transform refBone = reference.Bone;
        Vector3 neutral = reference.neutralAim;
        Vector3 forward = Vector3.ProjectOnPlane(neutral, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = hintAnchor.forward;

        Vector3 direction = targetPos - refBone.position;
        if (direction.sqrMagnitude < 0.001f) direction = neutral;

        // Avoid backwards/sideways twists: this correction is a forward bend, not a turn.
        float ahead = Mathf.Max(0.01f, Vector3.Dot(direction, forward));
        float pitch = Mathf.Atan2(-direction.y, ahead) * Mathf.Rad2Deg;
        if (targetPos.y < upperArm.position.y) pitch += extraDownwardLeanPitch;
        pitch = Mathf.Clamp(pitch, 0f, maxForwardLeanAngle);

        Vector3 desired = forward * Mathf.Cos(pitch * Mathf.Deg2Rad)
                        + Vector3.down * Mathf.Sin(pitch * Mathf.Deg2Rad);
        spineAimDirection = Vector3.RotateTowards(neutral, desired,
            Mathf.Clamp(maxForwardLeanAngle, 0f, 85f) * Mathf.Deg2Rad, 0f).normalized;
        UpdateSpineAim();
    }

    private void UpdateSpineAim()
    {
        if (spineChain != null && spineAimDirection.sqrMagnitude > 0.001f)
        {
            // Every vertebra aims along the same world direction, each from its own position.
            foreach (var link in spineChain)
                if (link != null && link.IsValid)
                    link.aimTarget.position = link.Bone.position + spineAimDirection;
        }

        if (HasTwist && isGrabbing)
            chestTwistTarget.position = postureTarget;
    }
}