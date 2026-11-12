using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class GrabIK : MonoBehaviour
{
    [Header("Rig References")]
    [SerializeField] private Rig armRig;
    [SerializeField] private Transform handTarget;
    [SerializeField] private Transform elbowHint;

    [Header("Timings")]
    [SerializeField] private GrabIKTimings timings;

    [Header("Bones")]
    [SerializeField] private Transform upperArm;
    [SerializeField] private Transform forearm;
    [SerializeField] public Transform hand;
    [SerializeField] private Transform holdPoint;

    [Header("IK Modules")]
    [SerializeField] private GrabIKSpineSettings spine = new GrabIKSpineSettings();
    [SerializeField] private GrabIKCrouchSettings crouch = new GrabIKCrouchSettings();
    [SerializeField] private GrabIKTwistSettings twist = new GrabIKTwistSettings();

    [Header("Debug")]
    [SerializeField] private bool gizmos;
    [SerializeField] private Vector3 handForward;
    [SerializeField] private Vector3 handUp;

    // State properties
    public float AppliedCrouchDepth => appliedCrouchDepth;
    public float LeanWeight => leanWeight;
    public float RequestedLeanWeight => requestedLeanWeight;
    public bool CrouchAuthorized => crouchAuthorized;
    public float ContactError { get; private set; }
    public bool IsReaching => isGrabbing;
    public Transform UpperArm => upperArm;
    public float SpineLeanAngle
    {
        get
        {
            var top = TopLink;
            return top != null && top.neutralAim.sqrMagnitude > 0.001f
                ? Vector3.Angle(top.neutralAim, top.Bone.TransformDirection(top.axis)) : 0f;
        }
    }

    // Internal State
    private float twistWeight, armLength, leanWeight, sampledLeanWeight, requestedLeanWeight, appliedCrouchDepth;
    private int sampleFrame = -1, stableLeanFrames, reachVersion;
    private bool isGrabbing, resolvingPosture, crouchAuthorized, feetPlanted, hintsCaptured;
    private bool extraLeanRequired, crouchTargetValid, crouchTargetConverged, leanDecided;
    private float crouchProgress, crouchTargetDepth;
    private Vector3 sampledShoulder, sampledLeanShoulder, previousLeanShoulder, postureTarget, reachStartPosition, spineAimDirection;
    private Quaternion reachStartRotation;
    private Vector3 leftHintLocal, rightHintLocal, elbowOffsetFromShoulder;
    private Vector3 leftPlantPos, rightPlantPos;
    private Quaternion leftPlantRot, rightPlantRot;
    private Transform hips;
    private Animator reachAnimator;
    private AnimatorCullingMode previousCullingMode;

    private void Awake()
    {
        reachAnimator = GetComponent<Animator>();
        if (timings == null)
        {
            Debug.LogWarning("GrabIK: no GrabIKTimings assigned, using defaults.", this);
            timings = ScriptableObject.CreateInstance<GrabIKTimings>();
        }

        armLength = upperArm != null && forearm != null && hand != null
            ? Vector3.Distance(upperArm.position, forearm.position)
                + Vector3.Distance(forearm.position, hand.position) + spine.armLengthOffset : 0f;

        if (armRig != null) armRig.weight = 0f;
        SetLeanWeight(0f);
        if (twist.chestConstraint != null) twist.chestConstraint.weight = 0f;
        if (crouch.hipsOverride != null) hips = crouch.hipsOverride.data.constrainedObject;

        if (crouch.hintAnchor == null) crouch.hintAnchor = transform.root;
        if (crouch.leftKneeHint != null) leftHintLocal = crouch.hintAnchor.InverseTransformPoint(crouch.leftKneeHint.position);
        if (crouch.rightKneeHint != null) rightHintLocal = crouch.hintAnchor.InverseTransformPoint(crouch.rightKneeHint.position);

        if (elbowHint != null && upperArm != null)
            elbowOffsetFromShoulder = crouch.hintAnchor.InverseTransformDirection(elbowHint.position - upperArm.position);

        hintsCaptured = true;
        SetCrouch(0f, 0f);
    }

    public void SetTargetPose(Vector3 position, Quaternion rotation)
    {
        if (handTarget != null) handTarget.SetPositionAndRotation(position, rotation);
    }

    public Pos GetCurrentHandPose()
    {
        hand.GetPositionAndRotation(out Vector3 p, out Quaternion r);
        return new Pos(p, r);
    }

    // Kinematic utility designed specifically for pointing logic
    public Pos CalculatePointPose(Vector3 targetPosition, float distanceOffset = 0.1f)
    {
        Vector3 shoulderPos = upperArm.position;
        Vector3 targetDir = (targetPosition - shoulderPos).normalized;
        Quaternion targetRot = GetHandRotationFacing(targetDir, Vector3.up);

        float currentArmLength = Vector3.Distance(shoulderPos, hand.position);
        Vector3 targetPos = shoulderPos + targetDir * (currentArmLength - distanceOffset);

        return new Pos(targetPos, targetRot);
    }

    private void Update()
    {
        if (hintsCaptured)
        {
            if (crouch.leftKneeHint != null) crouch.leftKneeHint.position = crouch.hintAnchor.TransformPoint(leftHintLocal);
            if (crouch.rightKneeHint != null) crouch.rightKneeHint.position = crouch.hintAnchor.TransformPoint(rightHintLocal);
            if (elbowHint != null && upperArm != null)
                elbowHint.position = upperArm.position + crouch.hintAnchor.TransformDirection(elbowOffsetFromShoulder);
        }

        if (feetPlanted)
        {
            if (crouch.leftFootTarget != null) crouch.leftFootTarget.SetPositionAndRotation(leftPlantPos, leftPlantRot);
            if (crouch.rightFootTarget != null) crouch.rightFootTarget.SetPositionAndRotation(rightPlantPos, rightPlantRot);
        }
    }

    private void LateUpdate()
    {
        if (!isGrabbing || upperArm == null) return;
        sampledShoulder = upperArm.position;
        Vector3 crouchOffset = Vector3.zero;

        if (crouch.hipsOverride != null && hips != null && crouch.rig != null)
            crouchOffset = hips.TransformDirection(crouch.hipsOverride.data.position)
                * crouch.hipsOverride.data.positionWeight * crouch.hipsOverride.weight * crouch.rig.weight;

        sampledLeanShoulder = sampledShoulder - crouchOffset;
        sampledLeanWeight = HasSpineLean ? leanWeight : 0f;
        bool leanEvaluated = !HasSpineLean || sampledLeanWeight >= requestedLeanWeight - 0.001f;

        stableLeanFrames = leanEvaluated && sampleFrame >= 0
            && Vector3.Distance(sampledLeanShoulder, previousLeanShoulder) <= spine.settleTolerance
            ? stableLeanFrames + 1 : 0;

        previousLeanShoulder = sampledLeanShoulder;
        sampleFrame = Time.frameCount;
        UpdateSpineAim();
    }

    private bool LeanSettled => !HasSpineLean || (leanDecided && stableLeanFrames >= 2);
    private bool CrouchInProgress => crouchAuthorized && crouchTargetDepth > 0f
        && (crouchProgress < 1f || !crouchTargetConverged || !LeanSettled);

    private void UpdateReachPosture(float progress)
    {
        float leanProgress = Mathf.Clamp01(progress / Mathf.Clamp(timings.leanFirstFraction, 0.1f, 0.9f));
        bool evaluated = sampleFrame >= 0 && sampleFrame < Time.frameCount && stableLeanFrames >= 2;

        if (!leanDecided && HasSpineLean && leanProgress >= 1f && evaluated)
        {
            leanDecided = true;
            if (Vector3.Distance(sampledLeanShoulder, postureTarget) > EffectiveArmLength + spine.reachTolerance)
                extraLeanRequired = true;
        }

        if (extraLeanRequired)
            requestedLeanWeight = Mathf.MoveTowards(requestedLeanWeight, Mathf.Clamp01(spine.maxWeight),
                Time.deltaTime / Mathf.Max(0.1f, timings.leanFirstFraction));

        if (HasSpineLean) SetLeanWeight(Mathf.SmoothStep(0f, requestedLeanWeight, leanProgress));

        if (!crouchAuthorized && sampleFrame >= 0)
        {
            float leanCompletion = LeanSettled ? 1f : Mathf.Min(0.999f, Mathf.SmoothStep(0f, 1f, leanProgress));
            if (leanCompletion >= Mathf.Clamp01(timings.crouchStartLeanCompletion)) crouchAuthorized = true;
        }
        if (!crouchAuthorized) return;

        float desiredDepth = CalculateCrouchDepth(postureTarget);
        if (!crouchTargetValid)
        {
            crouchTargetDepth = desiredDepth;
            crouchTargetValid = true;
        }
        else if (Mathf.Abs(desiredDepth - crouchTargetDepth) > spine.reachTolerance)
        {
            float retargetSpeed = Mathf.Max(0.01f, crouch.maxDepth) / Mathf.Max(0.01f, timings.crouchDuration);
            crouchTargetDepth = Mathf.MoveTowards(crouchTargetDepth, desiredDepth, retargetSpeed * Time.deltaTime);
        }
        crouchTargetConverged = Mathf.Abs(desiredDepth - crouchTargetDepth) <= spine.reachTolerance;

        if (crouchTargetDepth > 0f)
            crouchProgress = Mathf.MoveTowards(crouchProgress, 1f, Time.deltaTime / Mathf.Max(0.01f, timings.crouchDuration));

        appliedCrouchDepth = crouchTargetDepth * Mathf.Clamp01(timings.crouchCurve.Evaluate(crouchProgress));
        if (appliedCrouchDepth > 0f && !feetPlanted) PlantFeet();

        SetCrouch(appliedCrouchDepth, 1f);
    }

    public IEnumerator ReachRoutine(Func<Pos> targetPoseProvider, GrabProfile profile, Action onContact = null, GrabIKTimings timings = null)
    {
        if (isGrabbing || !isActiveAndEnabled || targetPoseProvider == null || profile == null || armRig == null)
        {
            Debug.LogWarning("Cannot start reach. Missing dependencies.");
            yield break;
        }
        if (timings == null) timings = this.timings;

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

            Pos gripTarget = targetPoseProvider();
            Matrix4x4 desiredHoldPose = PoseUtil.Matrix(gripTarget) * offset;
            Vector3 desiredHoldPos = desiredHoldPose.GetPosition();

            Vector3 startDir = (startPos - upperArm.position).normalized;
            Vector3 targetDir = (desiredHoldPos - upperArm.position);
            targetDir = targetDir.sqrMagnitude > 0.001f ? targetDir.normalized : Vector3.forward;

            Quaternion armSwing = Quaternion.FromToRotation(startDir, targetDir);
            Quaternion targetRot = armSwing * startRot;

            Vector3 rightAxis = Vector3.Cross(Vector3.up, targetDir).normalized;
            if (rightAxis.sqrMagnitude > 0.001f) targetRot = Quaternion.AngleAxis(-20f, rightAxis) * targetRot;

            Vector3 localHoldPos = holdInHand.GetPosition();
            Vector3 targetPos = desiredHoldPos - (targetRot * localHoldPos);

            postureTarget = targetPos;
            requestedLeanWeight = CalculateLeanWeight(targetPos);
            PrepareSpineAim(targetPos);

            ResetPostureState();

            float duration = Mathf.Max(0.01f, timings.reachDuration);
            float elapsed = 0f;
            int finalPoseFrames = 0;
            float settleTime = Mathf.Clamp(timings.maxReachSettleTime, 0f, 0.5f);
            float crouchTime = Mathf.Max(0.01f, timings.crouchDuration);

            while (elapsed < duration + settleTime || (CrouchInProgress && elapsed < duration + settleTime + crouchTime))
            {
                if (version != reachVersion || !isGrabbing || !isActiveAndEnabled) yield break;
                elapsed += Time.deltaTime;

                // Keep evaluating tracking target in case it moves dynamically
                gripTarget = targetPoseProvider();
                desiredHoldPose = PoseUtil.Matrix(gripTarget) * offset;
                desiredHoldPos = desiredHoldPose.GetPosition();

                targetDir = (desiredHoldPos - upperArm.position);
                targetDir = targetDir.sqrMagnitude > 0.001f ? targetDir.normalized : Vector3.forward;
                armSwing = Quaternion.FromToRotation(startDir, targetDir);
                targetRot = armSwing * startRot;
                rightAxis = Vector3.Cross(Vector3.up, targetDir).normalized;
                if (rightAxis.sqrMagnitude > 0.001f) targetRot = Quaternion.AngleAxis(-20f, rightAxis) * targetRot;
                targetPos = desiredHoldPos - (targetRot * localHoldPos);

                float progress = Mathf.Clamp01(elapsed / duration);
                float t = progress >= 1f ? 1f : Mathf.Clamp01(timings.reachCurve.Evaluate(progress));
                handTarget.SetPositionAndRotation(Vector3.Lerp(startPos, targetPos, t), Quaternion.Slerp(startRot, targetRot, t));

                armRig.weight = t;
                UpdateReachPosture(progress);
                UpdateTwist();

                if (progress >= 1f && sampleFrame >= 0 && !CrouchInProgress && Vector3.Distance(hand.position, targetPos) <= spine.reachTolerance)
                    finalPoseFrames++;
                else finalPoseFrames = 0;

                yield return null;
                if (finalPoseFrames >= 2) break;
            }

            resolvingPosture = false;
            if (version != reachVersion || !isGrabbing || !isActiveAndEnabled) yield break;
            handTarget.SetPositionAndRotation(targetPos, targetRot);
            armRig.weight = 1f;
            yield return null;
            if (version != reachVersion || !isGrabbing || !isActiveAndEnabled) yield break;

            ContactError = Vector3.Distance(hand.position, targetPos);
            onContact?.Invoke();

            float blendDuration = Mathf.Max(0.01f, timings.ikBlendOutDuration);
            float blendElapsed = 0f;
            float initialCrouchDepth = appliedCrouchDepth;
            float targetLean = requestedLeanWeight;

            while (blendElapsed < blendDuration)
            {
                if (version != reachVersion || !isGrabbing || !isActiveAndEnabled) yield break;
                blendElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blendElapsed / blendDuration));

                armRig.weight = 1f - t;
                appliedCrouchDepth = initialCrouchDepth * (1f - t);
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

    private void ResetPostureState()
    {
        resolvingPosture = true;
        appliedCrouchDepth = 0f;
        crouchProgress = 0f;
        crouchTargetDepth = 0f;
        crouchTargetValid = false;
        crouchTargetConverged = false;
        leanDecided = false;
        crouchAuthorized = false;
        stableLeanFrames = 0;
        sampleFrame = -1;
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
        feetPlanted = resolvingPosture = crouchAuthorized = extraLeanRequired = leanDecided = crouchTargetValid = crouchTargetConverged = false;
        sampleFrame = -1;
        stableLeanFrames = 0;
        appliedCrouchDepth = requestedLeanWeight = crouchProgress = crouchTargetDepth = 0f;
        spineAimDirection = Vector3.zero;

        ResetTwist();
        SetLeanWeight(0f);
        if (armRig != null) armRig.weight = 0f;
        if (handTarget != null && isGrabbing) handTarget.SetPositionAndRotation(reachStartPosition, reachStartRotation);
        isGrabbing = false;
    }

    public IEnumerator ArmReachPhase(Pos startPos, Pos targetPos, Action<float> onUpdate = null)
    {
        float duration = Mathf.Max(0.01f, timings.reachDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = timings.reachCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            handTarget.SetPositionAndRotation(Vector3.Lerp(startPos.p, targetPos.p, t), Quaternion.Slerp(startPos.r, targetPos.r, t));
            armRig.weight = t;
            onUpdate?.Invoke(t);
            yield return null;
        }
        armRig.weight = 1f;
    }

    public IEnumerator ArmDownPhase(Action<float> onUpdate = null)
    {
        float blendDuration = Mathf.Max(0.01f, timings.ikBlendOutDuration);
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

    private float EffectiveArmLength => Mathf.Max(0.01f, armLength - spine.overshoot);
    private float LeanMaxOverReach => EffectiveArmLength * (1f - spine.startRatio) + spine.reachBonus;

    private float CalculateCrouchDepth(Vector3 targetPos)
    {
        if (crouch.rig == null || hips == null || sampleFrame < 0) return 0f;
        float reach = Mathf.Max(0.01f, EffectiveArmLength + crouch.reachMargin);
        float needed = ResidualCrouchDepth(sampledLeanShoulder, targetPos, reach);
        if (needed < Mathf.Max(0f, crouch.minDepth)) return 0f;
        return Mathf.Min(needed, Mathf.Max(0f, crouch.maxDepth));
    }

    public static float ResidualCrouchDepth(Vector3 leanShoulder, Vector3 targetPos, float reach)
    {
        Vector3 delta = targetPos - leanShoulder;
        double horizontalSquared = (double)delta.x * delta.x + (double)delta.z * delta.z;
        double reachSquared = (double)reach * reach;
        if (double.IsNaN(horizontalSquared) || double.IsInfinity(horizontalSquared) || float.IsNaN(delta.y) || float.IsInfinity(delta.y)
            || float.IsNaN(reach) || float.IsInfinity(reach) || reach <= 0f || delta.y >= 0f || horizontalSquared > reachSquared) return 0f;

        float allowedBelow = (float)Math.Sqrt(Math.Max(0d, reachSquared - horizontalSquared));
        return Mathf.Max(0f, -delta.y - allowedBelow);
    }

    private void PlantFeet()
    {
        if (crouch.leftFoot != null && crouch.leftFootTarget != null)
        {
            crouch.leftFoot.GetPositionAndRotation(out leftPlantPos, out leftPlantRot);
            crouch.leftFootTarget.SetPositionAndRotation(leftPlantPos, leftPlantRot);
        }
        if (crouch.rightFoot != null && crouch.rightFootTarget != null)
        {
            crouch.rightFoot.GetPositionAndRotation(out rightPlantPos, out rightPlantRot);
            crouch.rightFootTarget.SetPositionAndRotation(rightPlantPos, rightPlantRot);
        }
        feetPlanted = true;
    }

    private void SetCrouch(float depth, float amount)
    {
        if (crouch.rig == null) return;
        amount = Mathf.Clamp01(amount);
        crouch.rig.weight = depth > 0f ? 1f : 0f;

        if (crouch.hipsOverride == null || hips == null) return;
        Vector3 localDown = hips.InverseTransformDirection(Vector3.down);
        var data = crouch.hipsOverride.data;
        data.position = localDown * (depth * amount);
        crouch.hipsOverride.data = data;
    }

    private bool HasTwist => twist.chestConstraint != null && twist.chestTarget != null && twist.chestConstraint.data.constrainedObject != null;

    private void UpdateTwist()
    {
        if (!HasTwist) return;
        float lean = spine.maxWeight > 0.001f ? LeanWeight / spine.maxWeight : 0f;
        float crouchAmt = crouch.maxDepth > 0.001f ? appliedCrouchDepth / crouch.maxDepth : 0f;
        float demand = Mathf.Clamp01(Mathf.Max(lean, crouchAmt)) * twist.maxWeight;
        twistWeight = Mathf.MoveTowards(twistWeight, demand, timings.twistResponseSpeed * Time.deltaTime);
        twist.chestConstraint.weight = twistWeight;
    }

    private void ResetTwist()
    {
        twistWeight = 0f;
        if (twist.chestConstraint != null) twist.chestConstraint.weight = 0f;
    }

    public Quaternion GetHandRotationFacing(Vector3 targetForward, Vector3 targetUp)
    {
        Quaternion worldAim = Quaternion.LookRotation(targetForward.normalized, targetUp.normalized);
        Quaternion localHandBasis = Quaternion.LookRotation(handForward, handUp);
        return worldAim * Quaternion.Inverse(localHandBasis);
    }

    private float CalculateLeanWeight(Vector3 targetPos)
    {
        if (!HasSpineLean) return 0f;
        float comfortableReach = EffectiveArmLength * Mathf.Clamp01(spine.startRatio);
        float overReach = Vector3.Distance(upperArm.position, targetPos) - comfortableReach;
        return Mathf.Clamp01(overReach / Mathf.Max(0.01f, LeanMaxOverReach)) * Mathf.Clamp01(spine.maxWeight);
    }

    private SpineLeanLink BaseLink
    {
        get
        {
            if (spine.chain == null) return null;
            for (int i = 0; i < spine.chain.Length; i++)
                if (spine.chain[i] != null && spine.chain[i].IsValid) return spine.chain[i];
            return null;
        }
    }

    private SpineLeanLink TopLink
    {
        get
        {
            if (spine.chain == null) return null;
            for (int i = spine.chain.Length - 1; i >= 0; i--)
                if (spine.chain[i] != null && spine.chain[i].IsValid) return spine.chain[i];
            return null;
        }
    }

    private bool HasSpineLean => BaseLink != null;

    private void SetLeanWeight(float weight)
    {
        leanWeight = Mathf.Clamp01(weight);
        if (spine.chain == null) return;
        foreach (var link in spine.chain)
            if (link != null && link.constraint != null)
                link.constraint.weight = leanWeight * link.weightScale;
    }

    private void PrepareSpineAim(Vector3 targetPos)
    {
        var reference = TopLink;
        if (reference == null) return;

        foreach (var link in spine.chain)
        {
            if (link == null || !link.IsValid) continue;
            link.axis = ((IMultiAimConstraintData)link.constraint.data).aimAxis;
            link.neutralAim = link.Bone.TransformDirection(link.axis).normalized;
        }

        Transform refBone = reference.Bone;
        Vector3 neutral = reference.neutralAim;
        Vector3 forward = Vector3.ProjectOnPlane(neutral, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = crouch.hintAnchor.forward;

        Vector3 direction = targetPos - refBone.position;
        if (direction.sqrMagnitude < 0.001f) direction = neutral;

        float ahead = Mathf.Max(0.01f, Vector3.Dot(direction, forward));
        float pitch = Mathf.Atan2(-direction.y, ahead) * Mathf.Rad2Deg;
        if (targetPos.y < upperArm.position.y) pitch += spine.extraDownwardPitch;
        pitch = Mathf.Clamp(pitch, 0f, spine.maxForwardAngle);

        Vector3 desired = forward * Mathf.Cos(pitch * Mathf.Deg2Rad) + Vector3.down * Mathf.Sin(pitch * Mathf.Deg2Rad);
        spineAimDirection = Vector3.RotateTowards(neutral, desired, Mathf.Clamp(spine.maxForwardAngle, 0f, 85f) * Mathf.Deg2Rad, 0f).normalized;
        UpdateSpineAim();
    }

    private void UpdateSpineAim()
    {
        if (spine.chain != null && spineAimDirection.sqrMagnitude > 0.001f)
        {
            foreach (var link in spine.chain)
                if (link != null && link.IsValid)
                    link.aimTarget.position = link.Bone.position + spineAimDirection;
        }
        if (HasTwist && isGrabbing) twist.chestTarget.position = postureTarget;
    }

    private void OnDrawGizmosSelected()
    {
        if (!gizmos || upperArm == null || forearm == null || hand == null) return;
        float currentArmLength = Application.isPlaying && armLength > 0f ? armLength :
            (Vector3.Distance(upperArm.position, forearm.position) + Vector3.Distance(forearm.position, hand.position) + spine.armLengthOffset);
        float effectiveLength = Mathf.Max(0.01f, currentArmLength - spine.overshoot);

        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(upperArm.position, effectiveLength * spine.startRatio);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(upperArm.position, effectiveLength);

        if (handTarget != null)
        {
            Gizmos.color = Color.yellow; Gizmos.DrawLine(upperArm.position, handTarget.position);
            Gizmos.DrawWireSphere(handTarget.position, 0.025f);
        }

        if (spine.chain != null && HasSpineLean)
        {
            Gizmos.color = Color.green;
            foreach (var link in spine.chain)
            {
                if (link == null || !link.IsValid) continue;
                Gizmos.DrawLine(link.Bone.position, link.aimTarget.position); Gizmos.DrawWireSphere(link.aimTarget.position, 0.025f);
            }
        }

        if (Application.isPlaying && isGrabbing)
        {
            Gizmos.color = Color.magenta; Gizmos.DrawLine(sampledLeanShoulder, sampledShoulder);
        }
    }
}