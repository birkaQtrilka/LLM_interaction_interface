using UnityEngine;

// The label names who would hear an order. Chat is unchanged
public class LookCast : MonoBehaviour
{
    [SerializeField] float radius = 0.35f;
    [SerializeField] float distance = 4f;
    [SerializeField] float socialRadius = 3.5f;

    Camera eyes;
    NPC[] nurses = System.Array.Empty<NPC>();
    string shown = "";
    GUIStyle label;

    void Awake()
    {
        eyes = GetComponent<Camera>();
    }

    void Start()
    {
        nurses = FindObjectsByType<NPC>();
    }

    void Update()
    {
        if (eyes == null) return;

        Vector3 origin = eyes.transform.position;
        Vector3 direction = eyes.transform.forward;
        Debug.DrawRay(origin, direction * distance, Color.green);

        string text = Describe(origin, direction);
        if (text == shown) return;
        shown = text;
        Debug.Log("Look cast: " + shown);
    }

    string Describe(Vector3 origin, Vector3 direction)
    {
        int nearby = 0;
        NPC only = null;
        for (int i = 0; i < nurses.Length; i++)
        {
            NPC nurse = nurses[i];
            if (nurse == null) continue;
            if (DistanceFrom(nurse, origin) > socialRadius) continue;
            nearby++;
            only = nurse;
        }

        if (nearby == 0) return "Nobody is close enough";
        if (nearby == 1) return "Only one nearby: " + only.name;

        if (!Physics.SphereCast(origin, radius, direction, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return "Look at who you mean";

        NPC looked = hit.collider.GetComponentInParent<NPC>();
        if (looked != null && DistanceFrom(looked, origin) <= socialRadius)
            return "Looking at: " + looked.name;

        // The first solid thing was not a nearby nurse, so the label shows what stopped the cast
        return "Look at who you mean: " + hit.collider.name;
    }

    static float DistanceFrom(NPC nurse, Vector3 origin)
    {
        CapsuleCollider capsule = nurse.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = nurse.GetComponentInChildren<CapsuleCollider>();
        if (capsule == null) return Vector3.Distance(origin, nurse.transform.position);
        return Vector3.Distance(origin, capsule.ClosestPoint(origin));
    }

    void OnGUI()
    {
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label);
            label.fontSize = 40;
        }
        GUI.Label(new Rect(12, 12, 1200, 64), shown, label);
    }
}
