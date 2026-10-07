using UnityEngine;

// The label names who would hear an order. A sent line uses that nurse
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

        string text = Evaluate().label;
        if (text == shown) return;
        shown = text;
        Debug.Log("Look cast: " + shown);
    }

    public bool TryChoose(out NPC nurse, out string reason)
    {
        Choice choice = Evaluate();
        nurse = choice.nurse;
        reason = choice.label;
        return nurse != null;
    }

    Choice Evaluate()
    {
        if (eyes == null)
            return new Choice { label = "No player camera, so nobody can be addressed" };
        return Describe(eyes.transform.position, eyes.transform.forward);
    }

    Choice Describe(Vector3 origin, Vector3 direction)
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

        if (nearby == 0) return new Choice { label = "Nobody is close enough" };
        if (nearby == 1) return new Choice { nurse = only, label = "Only one nearby: " + only.name };

        if (!Physics.SphereCast(origin, radius, direction, out RaycastHit hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return new Choice { label = "Look at who you mean" };

        NPC looked = hit.collider.GetComponentInParent<NPC>();
        if (looked != null && DistanceFrom(looked, origin) <= socialRadius)
            return new Choice { nurse = looked, label = "Looking at: " + looked.name };

        // The first solid thing was not a nearby nurse, so the label shows what stopped the cast
        return new Choice { label = "Look at who you mean: " + hit.collider.name };
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

    struct Choice
    {
        public NPC nurse;
        public string label;
    }
}
