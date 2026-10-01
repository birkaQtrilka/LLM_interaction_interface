using UnityEngine;
using UnityEditor;

public class ReservedSpotsDebugger : MonoBehaviour
{
    public bool draw = true;
    public bool showLabels = true;
    public float spotRadius = 0.25f;
    public float takenRadius = 0.9f; // matches spacing * 0.9 used by IsTaken, adjust to your spacing
    public Color spotColor = new Color(1f, 0.6f, 0f, 1f);
    public Color lineColor = new Color(1f, 0.6f, 0f, 0.4f);
    public Color destinationColor = Color.cyan;

    void OnDrawGizmos()
    {
        if (!draw || !Application.isPlaying) return;

        foreach (var kv in MovementActions.reserved)
        {
            NPC npc = kv.Key;
            if (npc == null) continue; // destroyed while still holding a reservation
            Vector3 spot = kv.Value;

            // the reserved spot and the area other agents are kept out of
            Gizmos.color = spotColor;
            Gizmos.DrawWireSphere(spot, spotRadius);
            Gizmos.DrawSphere(spot + Vector3.up * 0.02f, 0.06f);
            Gizmos.color = new Color(spotColor.r, spotColor.g, spotColor.b, 0.15f);
            Gizmos.DrawWireSphere(spot, takenRadius);

            // owner to spot
            Gizmos.color = lineColor;
            Gizmos.DrawLine(npc.transform.position + Vector3.up * 0.1f, spot);

            // what the NavMeshAgent is actually heading to
            if (npc.Nav != null && npc.Nav.hasPath)
            {
                Gizmos.color = destinationColor;
                Gizmos.DrawWireCube(npc.Nav.destination, Vector3.one * 0.15f);
            }

            if (showLabels)
                Handles.Label(spot + Vector3.up * 0.5f, npc.name);
        }
    }
}