using UnityEngine;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(NPC))]
public class NPCDebugVisualization : MonoBehaviour
{
    public NPC npc;

    [Header("Target Simulation")]
    [Tooltip("Assign a target to see how the NPC would calculate rings/spots around it. If null, uses the current NavMesh destination at runtime.")]
    public Transform debugTarget;

    [Header("Toggles")]
    public bool drawZones = true;
    public bool drawReserveRings = true;
    public bool drawGlobalReservations = true;
    public bool alwaysDraw = false;

    private void OnValidate()
    {
        if (npc == null) npc = GetComponent<NPC>();
    }

    private void OnDrawGizmos()
    {
        if (alwaysDraw) DrawAllGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!alwaysDraw) DrawAllGizmos();
    }

    private void DrawAllGizmos()
    {
        if (npc == null || npc.Nav == null) return;

#if UNITY_EDITOR
        if (drawZones) DrawZones();
        if (drawReserveRings) DrawRingsAndSpots();
        if (drawGlobalReservations) DrawGlobalReservations();
#endif
    }

#if UNITY_EDITOR
    private void DrawZones()
    {
        Vector3 origin = transform.position;
        origin.y += 0.05f; // Lift slightly above floor

        // 1. Raw Stopping Distance (Green Wireframe)
        Handles.color = Color.green;
        Handles.DrawWireDisc(origin, Vector3.up, npc.Nav.stoppingDistance);

        // 2. "Don't Move Just Rotate" Zone (Solid Red/Orange)
        // This is stoppingDistance + StoppingDistOffset
        Handles.color = new Color(1f, 0.3f, 0f, 0.15f);
        Handles.DrawSolidDisc(origin, Vector3.up, npc.Nav.stoppingDistance + npc.StoppingDistOffset);

        // 3. Face Distance Zone (Blue Outline)
        Handles.color = new Color(0f, 0.5f, 1f, 0.3f);
        Handles.DrawWireDisc(origin, Vector3.up, npc.FaceDistance);

        // Labels
        Handles.Label(origin + transform.right * npc.Nav.stoppingDistance, " Stopping Dist");
        Handles.Label(origin + transform.right * (npc.Nav.stoppingDistance + npc.StoppingDistOffset), " Rotate-Only Zone");
        Handles.Label(origin - transform.right * npc.FaceDistance, "Face Distance");
    }

    private void DrawRingsAndSpots()
    {
        Vector3 targetPos;
        bool isAgent = false;
        float targetRadius = 0f;

        // Determine what we are looking at
        if (debugTarget != null)
        {
            targetPos = debugTarget.position;
            if (debugTarget.TryGetComponent(out NavMeshAgent otherNav))
            {
                isAgent = true;
                targetRadius = otherNav.radius;
            }
        }
        else if (Application.isPlaying && npc.Nav.hasPath)
        {
            targetPos = npc.Nav.destination; // fallback to current pathing target
        }
        else
        {
            return;
        }

        // Snap target to navmesh for accuracy
        if (NavMesh.SamplePosition(targetPos, out var baseHit, 3f, NavMesh.AllAreas))
            targetPos = baseHit.position;

        // Calculate Start Radius (replicating MovementActions.Build logic)
        float standoff = npc.Nav.radius + npc.StandoffOffset;
        if (isAgent) standoff += targetRadius;

        float startRadius = (debugTarget != null || isAgent) ? standoff : 0f;

        // Base angle calculation
        Vector3 from = transform.position - targetPos;
        from.y = 0f;
        float baseAngle = from.sqrMagnitude > 0.001f
            ? Mathf.Atan2(from.z, from.x)
            : Mathf.Atan2(-transform.forward.z, -transform.forward.x);

        // Draw the rings & spots
        for (int ring = 0; ring < npc.ReserveRings; ring++)
        {
            float radius = startRadius + ring * npc.ReserveSpacing;

            // Draw Ring Line
            Handles.color = new Color(1f, 1f, 1f, 0.2f);
            Handles.DrawWireDisc(targetPos, Vector3.up, radius);

            int count = Mathf.Max(6, ring * 6);
            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + i * Mathf.PI * 2f / count;
                Vector3 candidate = targetPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                // Validate on NavMesh
                if (!NavMesh.SamplePosition(candidate, out var hit, npc.ReserveSpacing, NavMesh.AllAreas))
                {
                    Gizmos.color = Color.gray; // Off NavMesh
                    Gizmos.DrawCube(candidate, Vector3.one * 0.15f);
                    continue;
                }

                // Check reservation
                bool isTaken = MovementReservation.IsTaken(npc, hit.position, npc.ReserveSpacing * 0.9f);
                if (isTaken)
                {
                    Handles.color = new Color(1f, 0f, 0f, 0.5f); // Red = Taken
                    Handles.DrawSolidDisc(hit.position, Vector3.up, 0.2f);
                }
                else
                {
                    Handles.color = new Color(0f, 1f, 0f, 0.4f); // Green = Free
                    Handles.DrawSolidDisc(hit.position, Vector3.up, 0.15f);
                }
            }
        }

        // Draw the Approach Point (if just approaching a transform)
        if (debugTarget != null && !isAgent)
        {
            Vector3 approach = MovementReservation.ApproachPoint(npc, debugTarget, npc.ApproachGap);
            Handles.color = Color.cyan;
            Handles.DrawLine(debugTarget.position, approach, 2f);
            Handles.DrawSolidDisc(approach, Vector3.up, 0.2f);
            Handles.Label(approach, " Approach Point");
        }
    }

    private void DrawGlobalReservations()
    {
        if (MovementReservation.Reserved == null) return;

        foreach (var kvp in MovementReservation.Reserved)
        {
            NPC owner = kvp.Key;
            Vector3 spot = kvp.Value;

            // Draw a line from the owner to their reserved spot
            if (owner != null)
            {
                Handles.color = (owner == npc) ? Color.yellow : new Color(1, 0, 1, 0.3f);
                Handles.DrawDottedLine(owner.transform.position, spot, 4f);
            }

            Handles.color = new Color(1f, 0f, 1f, 0.6f); // Magenta globally taken spots
            Handles.DrawWireDisc(spot, Vector3.up, 0.3f);
        }
    }
#endif
}