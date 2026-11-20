using System;
using System.Collections;
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
    [field: SerializeField] public float TurnSpeed { get; private set; } = 400f;      // Degrees per second the script rotates the NPC
    [field: SerializeField] public float MaxTurnAngle { get; private set; } = 60f;
    [Tooltip("If the angle to the target is less than this, the NPC will do a lerp rotation instead of animation")]
    [field: SerializeField] public float AngleForLerpRotation { get; private set; } = 7f;
    [field: SerializeField] public float FaceDistance { get; private set; } = 2f;     // Distance to item where we start facing the item
    [field: SerializeField] public float DampTime { get; private set; } = 0.15f;
    [field: SerializeField, Tooltip("While walking the NPC faces a point this far ahead along its path, so it turns before a corner instead of at it")]
    public float LookAheadDistance { get; private set; } = 1f;
    [field: SerializeField, Tooltip("Extra space around a standing NPC that walking NPCs keep clear of")]
    public float ObstaclePadding { get; private set; } = 0.25f;
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
    [field: SerializeField] public EmotionalStateHandler EmotionalStateHandler { get; private set; }

    // While standing still the NPC is a carving obstacle instead of an agent, so walking agents
    // plan their path around it instead of pushing it away with local avoidance
    NavMeshObstacle obstacle;

    private void Awake()
    {
        // Built from the agent settings so the obstacle always matches the agent size
        obstacle = Nav.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        // Padding moves the path corner away from the body, so passing NPCs keep some space
        obstacle.radius = Nav.radius + ObstaclePadding;
        obstacle.height = Nav.height;
        // The agent pivot sits baseOffset above the ground, the obstacle center is relative to that pivot
        obstacle.center = Vector3.up * (Nav.height / 2f - Nav.baseOffset);
        obstacle.carving = true;
        // Carve right away instead of waiting for the obstacle to count as stationary
        obstacle.carveOnlyStationary = false;

        // Rotation is done by the movement scripts: look-ahead while walking, turn animations when standing
        Nav.updateRotation = false;

        BecomeObstacle();
    }

    /// Switch from carving obstacle to agent so the NPC can walk. Must finish before using Nav
    public IEnumerator BecomeMover()
    {
        if (Nav.enabled) yield break;

        obstacle.enabled = false;
        // The hole is only removed on the next NavMesh carving update. Enabling the agent before
        // that would snap it to the edge of its own hole
        yield return null;
        Nav.enabled = true;

        if (!Nav.isOnNavMesh)
            Debug.LogError($"{name} is not on the NavMesh after switching from obstacle to agent");
    }

    /// Switch from agent to carving obstacle once the NPC stands still
    public void BecomeObstacle()
    {
        Nav.enabled = false;
        obstacle.enabled = true;
    }

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