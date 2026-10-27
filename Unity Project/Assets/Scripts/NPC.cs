using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[SelectionBase]
public class NPC : MonoBehaviour
{
    [field: SerializeField] public Transform RightHand { get; private set; }
    [field: SerializeField] public Transform LeftHand { get; private set; }
    [field: SerializeField] public Transform Head { get; private set; }
    [field: SerializeField] public NavMeshAgent Nav { get; private set; }
    [field: SerializeField] public GrabReceiver GrabReceiver { get; private set; }
    [field: SerializeField] public Animator Anim { get; private set; }
    [field: SerializeField] public LookSettings LookSettings { get; private set; } = new LookSettings { MaxSightDistance = 6f, CheckInterval = 0.1f, ItemTolerance = 0.5f };

    [Header("Movement Settings")]
    [field: SerializeField] public float WalkSpeedMult { get; private set; } = 1f;
    [field: SerializeField] public float TurnSpeed {get; private set; }= 400f;      // Degrees per second the script rotates the NPC
    [field: SerializeField] public float MaxTurnAngle {get; private set; }= 60f;    // Angle that produces maximum h (1.0 or -1.0)
    [field: SerializeField] public float FaceDistance {get; private set; }= 2f;     // Distance to item where we start facing the item
    [field: SerializeField] public float DampTime {get; private set; }= 0.15f;

    [Header("Turn Angle Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float gizmoHeightOffset = -0.1f;
    [SerializeField] private float gizmoRadius = 1.5f;
    [SerializeField, Range(8, 64)] private int gizmoSegments = 32;


    public event Action<Collision> OnCollide;

    public bool RightHandTaken => RightHand.childCount > 0;

    public bool LeftHandTaken => LeftHand.childCount > 0;


    public void GrabItem(Transform item, bool right)
    {
        if (right && !RightHandTaken)
        {
            item.SetParent(RightHand);
            item.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        else if (!right && !LeftHandTaken)
        {
            item.SetParent(LeftHand);
            item.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("Hand is full");
        }
    }

    public Transform ReleaseItem(bool right)
    {
        var item = GetItem(right);
        if (item == null) return null;
        item.SetParent(null);
        return item;
    }

    public Transform GetItem(bool right)
    {
        if (right && RightHandTaken)
        {
            return RightHand.GetChild(0);
        }
        else if (!right && LeftHandTaken)
        {
            return LeftHand.GetChild(0);
        }
        return null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        OnCollide?.Invoke(collision);
    }
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        Vector3 origin = transform.position + Vector3.up * gizmoHeightOffset;
        Vector3 forward = transform.forward;

        // Draw the forward direction
        Gizmos.color = Color.green;
        Gizmos.DrawRay(origin, forward * gizmoRadius);

        // Draw the left and right maximum turn boundaries
        Vector3 leftBoundary = Quaternion.AngleAxis(-MaxTurnAngle, Vector3.up) * forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(MaxTurnAngle, Vector3.up) * forward;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, leftBoundary * gizmoRadius);
        Gizmos.DrawRay(origin, rightBoundary * gizmoRadius);

        // Draw the arc between the boundaries
        Gizmos.color = Color.cyan;

        Vector3 previousPoint = origin + leftBoundary * gizmoRadius;

        for (int i = 1; i <= gizmoSegments; i++)
        {
            float angle = Mathf.Lerp(-MaxTurnAngle, MaxTurnAngle, i / (float)gizmoSegments);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 currentPoint = origin + direction * gizmoRadius;

            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
}
