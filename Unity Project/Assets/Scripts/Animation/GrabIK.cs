using NUnit.Framework.Constraints;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.XR;

public class GrabIK : MonoBehaviour
{
    [SerializeField] Transform test;
    Vector3 testInitPos;
    [SerializeField] Transform handTarget;
    [SerializeField] Transform holdPoint;
    [SerializeField] Rig grabRig;
    public float targetSpeed = 2f;
    public float weightSpeed = 2f;
    public float rotateSpeed = 360f;
    public float reachThreshold = 0.02f;

    bool isGrabbing;

    [Header("Body lean")]
    [SerializeField] Transform characterRoot;
    [SerializeField] ChainIKConstraint leanConstraint;
    [SerializeField] Transform leanTarget;
    [SerializeField] Transform upperArm;
    [SerializeField] Transform forearm;
    [SerializeField] Transform hand;
    [SerializeField, Range(0.3f, 1f)] float leanStartRatio = 0.7f;
    [SerializeField] float maxLeanDistance = 0.35f;
    [SerializeField] float leanSpeed = 3f;
    [SerializeField, Range(0f, 1f)] float verticalLeanScale = 0.5f;
    float armLength;
    float currentLean;

    Vector3 restShoulderLocal;
    void Awake()
    {
        testInitPos = test.position;
        armLength = Vector3.Distance(upperArm.position, forearm.position)
                  + Vector3.Distance(forearm.position, hand.position);
        leanConstraint.weight = 0f;
    }

    [ContextMenu("Grab Test")]
    public void GrabTest()
    {
        Resett();
        StartCoroutine(Grab(test));
    }

    [ContextMenu("Reset Item")]

    public void Resett()
    {
        test.parent = null;
        test.position = testInitPos;
    }

    public IEnumerator Grab(Transform t)
    {
        if (isGrabbing || t == null) yield break;
        isGrabbing = true;

        // Capture the shoulder in its current, real pose
        restShoulderLocal = characterRoot.InverseTransformPoint(upperArm.position);
        currentLean = 0f;

        handTarget.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        //grabRig.weight = 0f;

        while (Vector3.Distance(handTarget.position, t.position) > reachThreshold || grabRig.weight < 1.0f)
        {
            float dt = Time.deltaTime;
            handTarget.SetPositionAndRotation(
                Vector3.MoveTowards(handTarget.position, t.position, targetSpeed * dt), 
                Quaternion.RotateTowards(handTarget.rotation, t.rotation, rotateSpeed * dt));
            grabRig.weight = Mathf.MoveTowards(grabRig.weight, 1f, weightSpeed * dt);
            UpdateLean(t.position, true);
            yield return null;
        }
        grabRig.weight = 1f;

        // Attach the item to the hand
        if (t.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }
        t.SetParent(holdPoint != null ? holdPoint : handTarget, true);
        t.localPosition = Vector3.zero;

        // Bring the hand back to where it started while blending the rig off
        while (Vector3.Distance(handTarget.position, startPos) > reachThreshold || currentLean > 0.001f)
        {
            float dt = Time.deltaTime;
            handTarget.SetPositionAndRotation(
                Vector3.MoveTowards(handTarget.position, startPos, targetSpeed * dt),
                Quaternion.RotateTowards(handTarget.rotation, startRot, rotateSpeed * dt)
            );
            grabRig.weight = Mathf.MoveTowards(grabRig.weight, 0f, weightSpeed * dt);
            UpdateLean(t.position, false);
            yield return null;
        }

        handTarget.SetPositionAndRotation(startPos, startRot);
        currentLean = 0f;
        //grabRig.weight = 0f;
        isGrabbing = false;
    }

    void UpdateLean(Vector3 itemPos, bool active)
    {
        Vector3 shoulderRest = characterRoot.TransformPoint(restShoulderLocal);
        Vector3 toItem = itemPos - shoulderRest;
        float dist = toItem.magnitude;

        // Lean only by the distance the arm cannot reach comfortably
        float comfortable = armLength * leanStartRatio;
        float desiredLean = active ? Mathf.Clamp(dist - comfortable, 0f, maxLeanDistance) : 0f;

        currentLean = Mathf.MoveTowards(currentLean, desiredLean, leanSpeed * Time.deltaTime);

        Vector3 dir = toItem / Mathf.Max(dist, 0.0001f);
        dir.y *= verticalLeanScale;

        leanTarget.position = shoulderRest + dir * currentLean;
        leanConstraint.weight = Mathf.Clamp01(currentLean / 0.05f); // fades in and out with the lean
    }
    void OnDrawGizmos()
    {
        if (leanTarget == null || characterRoot == null) return;
        Vector3 rest = characterRoot.TransformPoint(restShoulderLocal);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(rest, 0.03f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(leanTarget.position, 0.05f);
        Gizmos.DrawLine(rest, leanTarget.position);
    }
}