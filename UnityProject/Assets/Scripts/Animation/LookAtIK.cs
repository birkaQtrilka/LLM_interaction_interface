using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LookAtIK : MonoBehaviour
{
    [SerializeField] private Rig aimConstraint;
    [Tooltip("Optional head pivot. Auto-fills from the last MultiAimConstraint's constrainedObject if left empty.")]
    [SerializeField] private Transform headPivot;
    [Tooltip("Character root whose blue (+Z) arrow faces forward and green (+Y) arrow faces up.")]
    [SerializeField] private Transform bodyTransform;

    [Header("Smoothing")]
    [SerializeField] private float targetSmoothTime = 0.2f;
    [Tooltip("Maximum head turn speed in degrees/sec (0 = unlimited).")]
    [SerializeField] private float maxTurnSpeed = 240f;
    [Tooltip("Fixed distance to keep the floater from the head (avoids Spine/Neck/Head parallax flips).")]
    [SerializeField] private float floaterDistance = 3f;

    [Header("Limits")]
    [Tooltip("Automatically reads yaw/pitch limits from single-axis MultiAimConstraints using their Aim/Up axes.")]
    [SerializeField] private bool readLimitsFromConstraints = true;
    [SerializeField] private Vector2 yawLimits = new Vector2(-75f, 75f);
    [SerializeField] private Vector2 pitchLimits = new Vector2(-25f, 25f);

    private Coroutine weightRoutine;
    private float weightGoal = -1f;
    private Transform floater;

    private Vector3 currentDir = Vector3.forward;
    private Vector3 lastValidTargetDir = Vector3.forward;
    private float behindYawSign = 1f;

    private void Awake()
    {
        EnsureFloaterExists();
    }

    private void EnsureFloaterExists()
    {
        if (floater != null) return;

        string floaterName = $"LookAtFloater_{gameObject.name}";
        var go = new GameObject(floaterName);
        floater = go.transform;
        floater.position = transform.position + transform.forward * floaterDistance;

        if (aimConstraint != null)
        {
            var aims = aimConstraint.GetComponentsInChildren<MultiAimConstraint>();
            foreach (var aim in aims)
            {
                var sources = aim.data.sourceObjects;

                bool alreadyPresent = false;
                for (int i = 0; i < sources.Count; i++)
                {
                    if (sources.GetTransform(i) == floater)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    sources.Add(new WeightedTransform(floater, 1f));
                    aim.data.sourceObjects = sources;
                }
            }

            if (readLimitsFromConstraints)
                ReadLimits(aims);

            if (headPivot == null)
            {
                for (int i = aims.Length - 1; i >= 0; i--)
                {
                    if (aims[i].data.constrainedObject != null)
                    {
                        headPivot = aims[i].data.constrainedObject;
                        break;
                    }
                }
            }

            var rigBuilder = GetComponentInParent<RigBuilder>();
            if (bodyTransform == null)
                bodyTransform = rigBuilder != null ? rigBuilder.transform : transform;

            if (rigBuilder != null)
                rigBuilder.Build();
        }
        else if (bodyTransform == null)
        {
            bodyTransform = transform;
        }

        lastValidTargetDir = bodyTransform.forward;
        currentDir = bodyTransform.forward;
    }

    private void ReadLimits(MultiAimConstraint[] aims)
    {
        foreach (var aim in aims)
        {
            var d = aim.data;
            int axisCount = (d.constrainedXAxis ? 1 : 0) + (d.constrainedYAxis ? 1 : 0) + (d.constrainedZAxis ? 1 : 0);
            if (axisCount != 1) continue; // Multi-axis constraint: rely on Inspector yawLimits/pitchLimits

            Vector3 constrainedAxis = d.constrainedXAxis ? Vector3.right
                                    : d.constrainedYAxis ? Vector3.up
                                    : Vector3.forward;

            Vector3 aimVec = AxisToVector(d.aimAxis);
            Vector3 upVec = AxisToVector(d.upAxis);
            Vector3 rightVec = Vector3.Cross(upVec, aimVec);

            // Rotating around local Up = Yaw; rotating around local Right = Pitch
            if (Mathf.Abs(Vector3.Dot(constrainedAxis, upVec)) > 0.9f)
                yawLimits = d.limits;
            else if (Mathf.Abs(Vector3.Dot(constrainedAxis, rightVec)) > 0.9f)
                pitchLimits = d.limits;
        }
    }

    private static Vector3 AxisToVector(MultiAimConstraintData.Axis axis) => axis switch
    {
        MultiAimConstraintData.Axis.X => Vector3.right,
        MultiAimConstraintData.Axis.X_NEG => Vector3.left,
        MultiAimConstraintData.Axis.Y => Vector3.up,
        MultiAimConstraintData.Axis.Y_NEG => Vector3.down,
        MultiAimConstraintData.Axis.Z => Vector3.forward,
        MultiAimConstraintData.Axis.Z_NEG => Vector3.back,
        _ => Vector3.forward
    };

    private void OnDestroy()
    {
        if (floater != null && floater.gameObject != null)
            Destroy(floater.gameObject);
    }

    /// Call every frame. Snaps while the look isn't engaged, rotates smoothly otherwise.
    public void SetTarget(Vector3 pos)
    {
        EnsureFloaterExists();

        Vector3 origin = headPivot != null ? headPivot.position : transform.position;
        Vector3 toTarget = pos - origin;
        float dist = toTarget.magnitude;

        Vector3 rawTargetDir = dist > 0.35f ? toTarget / dist : lastValidTargetDir;
        lastValidTargetDir = rawTargetDir;

        Vector3 targetDir = ClampToViewCone(rawTargetDir, updateHysteresis: true);

        bool snap = aimConstraint == null || aimConstraint.weight < 0.005f;
        if (snap)
        {
            currentDir = targetDir;
            floater.position = origin + currentDir * floaterDistance;
            return;
        }

        float smoothT = targetSmoothTime > 0.001f
            ? 1f - Mathf.Exp(-Time.deltaTime / targetSmoothTime)
            : 1f;

        Vector3 desiredDir = Vector3.Slerp(currentDir, targetDir, smoothT).normalized;

        if (maxTurnSpeed > 0f)
        {
            float maxRadians = maxTurnSpeed * Mathf.Deg2Rad * Time.deltaTime;
            currentDir = Vector3.RotateTowards(currentDir, desiredDir, maxRadians, 0f).normalized;
        }
        else
        {
            currentDir = desiredDir;
        }

        currentDir = ClampToViewCone(currentDir, updateHysteresis: false);
        floater.position = origin + currentDir * floaterDistance;
    }

    private Vector3 ClampToViewCone(Vector3 worldDir, bool updateHysteresis)
    {
        Transform refT = bodyTransform != null ? bodyTransform : transform;
        Vector3 local = refT.InverseTransformDirection(worldDir).normalized;

        float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float horiz = Mathf.Sqrt(local.x * local.x + local.z * local.z);
        float pitch = Mathf.Atan2(local.y, horiz) * Mathf.Rad2Deg;

        // Keep strictly inside the front hemisphere so Slerp never crosses the +/-180 back seam
        float minYaw = Mathf.Max(-89f, yawLimits.x);
        float maxYaw = Mathf.Min(89f, yawLimits.y);
        float minPitch = Mathf.Max(-85f, pitchLimits.x);
        float maxPitch = Mathf.Min(85f, pitchLimits.y);

        if (updateHysteresis)
        {
            if (Mathf.Abs(yaw) > 155f)
            {
                yaw = behindYawSign >= 0f ? maxYaw : minYaw;
            }
            else
            {
                if (Mathf.Abs(yaw) > 5f)
                    behindYawSign = Mathf.Sign(yaw);

                yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
            }
        }
        else
        {
            yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        float yawRad = yaw * Mathf.Deg2Rad;
        float pitchRad = pitch * Mathf.Deg2Rad;
        float cosPitch = Mathf.Cos(pitchRad);

        Vector3 clampedLocal = new Vector3(
            Mathf.Sin(yawRad) * cosPitch,
            Mathf.Sin(pitchRad),
            Mathf.Cos(yawRad) * cosPitch
        );

        return refT.TransformDirection(clampedLocal).normalized;
    }

    public void Engage(float duration, AnimationCurve curve) => BlendTo(1f, duration, curve);
    public void Disengage(float duration, AnimationCurve curve) => BlendTo(0f, duration, curve);

    private void LateUpdate()
    {
        if (aimConstraint != null && weightRoutine == null && weightGoal >= 0f)
            aimConstraint.weight = weightGoal;
    }

    private void BlendTo(float goal, float duration, AnimationCurve curve)
    {
        bool atGoal = aimConstraint == null || Mathf.Approximately(aimConstraint.weight, goal);
        if (Mathf.Approximately(weightGoal, goal) && atGoal) return;

        weightGoal = goal;
        if (weightRoutine != null) StopCoroutine(weightRoutine);
        weightRoutine = StartCoroutine(BlendRoutine(goal, Mathf.Max(0.01f, duration), curve));
    }

    private IEnumerator BlendRoutine(float goal, float duration, AnimationCurve curve)
    {
        if (aimConstraint == null) yield break;
        float start = aimConstraint.weight, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float t = curve != null ? curve.Evaluate(k) : k;
            aimConstraint.weight = Mathf.Lerp(start, goal, t);
            yield return null;
        }
        aimConstraint.weight = goal;
        weightRoutine = null;
    }
}