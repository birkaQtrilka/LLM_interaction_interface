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
    [SerializeField] private Transform hand;

    [Header("Spine Lean / Aim")]
    [Tooltip("MultiAimConstraint configured on Spine/Chest bones")]
    [SerializeField] private MultiAimConstraint spineAimConstraint;
    [Tooltip("Transform the spine aims toward")]
    [SerializeField] private Transform spineAimTarget;
    [SerializeField, Range(0.5f, 1f)] private float leanStartRatio = 0.75f;
    [SerializeField, Range(0.1f, 1f)] private float maxLeanWeight = 0.85f;

    [Header("Motion Settings")]
    [SerializeField] private float reachDuration = 0.6f;
    [SerializeField] private float returnDuration = 0.5f;
    [SerializeField] private AnimationCurve reachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Attachment")]
    [SerializeField] private Transform holdPoint;

    private float armLength;
    private bool isGrabbing;

    [Header("Testing")]
    [SerializeField] Transform testTarget;
    Vector3 testTargetInitPos;

    private void Awake()
    {
        // Measure total arm length
        armLength = Vector3.Distance(upperArm.position, forearm.position)
                  + Vector3.Distance(forearm.position, hand.position);

        if (armRig != null) armRig.weight = 0f;
        if (spineAimConstraint != null) spineAimConstraint.weight = 0f;

        SetTestPosition();
    }
    
    [ContextMenu("Grab")]
    public void TestGrab()
    {
        ResetTestPosition();
        TriggerGrab(testTarget);
    }

    [ContextMenu("Set test position")]
    public void SetTestPosition()
    {
        testTargetInitPos = testTarget.position;
    }

    [ContextMenu("Reset test position")]
    public void ResetTestPosition()
    {
        testTarget.SetParent(null);
        testTarget.position = testTargetInitPos;
    }

    public void TriggerGrab(Transform targetItem)
    {
        if (!isGrabbing && targetItem != null)
        {
            StartCoroutine(GrabRoutine(targetItem));
        }
    }

    private IEnumerator GrabRoutine(Transform item)
    {
        isGrabbing = true;

        hand.GetPositionAndRotation(out Vector3 initialHandPos, out Quaternion initialHandRot);
        handTarget.SetPositionAndRotation(initialHandPos, initialHandRot);
        item.GetPositionAndRotation(out Vector3 targetItemPos, out Quaternion targetItemRot);

        float distanceToTarget = Vector3.Distance(upperArm.position, targetItemPos);
        float comfortableReach = armLength * leanStartRatio;

        // Calculate lean weight proportional to overextension
        float targetLeanWeight = 0f;
        if (distanceToTarget > comfortableReach)
        {
            float overReach = distanceToTarget - comfortableReach;
            float maxOverReach = armLength * (1f - leanStartRatio) + 0.3f;
            targetLeanWeight = Mathf.Clamp01(overReach / maxOverReach) * maxLeanWeight;
        }

        if (spineAimTarget != null)
        {
            spineAimTarget.position = targetItemPos;
        }

        // ================= REACH PHASE =================
        float elapsed = 0f;
        while (elapsed < reachDuration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / reachDuration);
            float curvedT = reachCurve.Evaluate(rawT);

            handTarget.SetPositionAndRotation(Vector3.Lerp(initialHandPos, targetItemPos, curvedT), Quaternion.Slerp(initialHandRot, targetItemRot, curvedT));

            armRig.weight = curvedT;
            if (spineAimConstraint != null)
            {
                spineAimConstraint.weight = Mathf.Lerp(0f, targetLeanWeight, curvedT);
            }

            yield return null;
        }

        handTarget.position = targetItemPos;
        armRig.weight = 1f;
        if (spineAimConstraint != null) spineAimConstraint.weight = targetLeanWeight;

        // ================= ATTACH ITEM =================
        if (item.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }

        Transform attachParent = holdPoint != null ? holdPoint : handTarget;
        item.SetParent(attachParent, true); // Keep relative offset so it doesn't pop

        // ================= RETURN PHASE =================
        Vector3 reachEndPos = handTarget.position;
        Quaternion reachEndRot = handTarget.rotation;
        float finalLeanWeight = spineAimConstraint != null ? spineAimConstraint.weight : 0f;

        elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / returnDuration);
            float curvedT = returnCurve.Evaluate(rawT);

            handTarget.SetPositionAndRotation(Vector3.Lerp(reachEndPos, initialHandPos, curvedT), Quaternion.Slerp(reachEndRot, initialHandRot, curvedT));
            armRig.weight = 1f - curvedT;
            if (spineAimConstraint != null)
            {
                spineAimConstraint.weight = Mathf.Lerp(finalLeanWeight, 0f, curvedT);
            }

            yield return null;
        }

        // Reset state
        armRig.weight = 0f;
        if (spineAimConstraint != null) spineAimConstraint.weight = 0f;
        handTarget.SetPositionAndRotation(initialHandPos, initialHandRot);
        isGrabbing = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (upperArm == null || forearm == null || hand == null) return;

        float length = Vector3.Distance(upperArm.position, forearm.position)
                     + Vector3.Distance(forearm.position, hand.position);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(upperArm.position, length * leanStartRatio);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(upperArm.position, length);
    }
}