using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[SelectionBase]
public class NPC : MonoBehaviour
{
    [field: SerializeField] public Transform RightHand { get; private set; }
    [field: SerializeField] public Transform Head { get; private set; }
    [field: SerializeField] public NavMeshAgent Nav { get; private set; }
    [field: SerializeField] public GrabReceiver GrabReceiver { get; private set; }
    [field: SerializeField] public Animator Anim { get; private set; }
    [field: SerializeField] public LookSettings LookSettings { get; private set; } = new LookSettings { MaxSightDistance = 6f, CheckInterval = 0.1f, ItemTolerance = 0.5f };
    [Header("Point Settings")]
    [field: SerializeField] public GrabProfile pointProfile { get; private set; }


    [Header("Movement Settings")]
    [field: SerializeField] public float WalkSpeedMult { get; private set; } = 1f;
    [field: SerializeField] public float TurnSpeed {get; private set; }= 400f;      // Degrees per second the script rotates the NPC
    [field: SerializeField] public float MaxTurnAngle {get; private set; }= 60f;
    [Tooltip("If the angle to the target is less than this, the NPC will do a lerp rotation instead of animation")]
    [field: SerializeField] public float AngleForLerpRotation {get; private set; }= 7f;
    [field: SerializeField] public float FaceDistance {get; private set; }= 2f;     // Distance to item where we start facing the item
    [field: SerializeField] public float DampTime {get; private set; }= 0.15f;
    [Header("Reservation Tweaks")]
    [field: SerializeField, Tooltip("Extra gap when approaching an object")]
    public float ApproachGap { get; private set; } = 0.3f;

    [field: SerializeField, Tooltip("Standoff padding when pathing to a target")]
    public float StandoffOffset { get; private set; } = 0.5f;

    [field: SerializeField, Tooltip("Tolerance added to stopping distance (Don't move just rotate zone)")]
    public float StoppingDistOffset { get; private set; } = 0.05f;

    [field: SerializeField, Tooltip("Distance between reserve rings")]
    public float ReserveSpacing { get; private set; } = 1f;

    [field: SerializeField, Tooltip("Number of reserve rings to generate")]
    public int ReserveRings { get; private set; } = 4;


    [Header("Turn Angle Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float gizmoHeightOffset = -0.1f;
    [SerializeField] private float gizmoRadius = 1.5f;
    [SerializeField, Range(8, 64)] private int gizmoSegments = 32;
    [field: SerializeField] public HandController Hand { get; private set; }

    public event Action<Collision> OnCollide;
    public bool RightHandTaken => Hand != null && Hand.Held != null;

    public void GrabItem(Transform item)
    {
        if (!RightHandTaken)
        {
            GrabbableItem grabbable = item.GetComponent<GrabbableItem>();
            if (grabbable == null || grabbable.profile == null)
            {
                Debug.LogWarning($"{item.name} is not grabbable");
                return;
            }
            if (RightHandTaken) { Debug.LogWarning("Hand is full"); return; }
            Hand.AttachInstant(grabbable);
        }
        else
        {
            Debug.LogWarning("Hand is full");
        }
    }

    public Transform ReleaseItem()
    {
        var item = GetItem();
        if (item == null) return null;
        Hand.ForceRelease();
        return item;
    }

    public Transform GetItem()
    {
        if (RightHandTaken)
        {
            return Hand.Held.transform;
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
