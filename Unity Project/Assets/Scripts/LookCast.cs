using System;
using UnityEngine;

// The label names who the look would pick
// A sent line uses a name in the sentence first, then that look
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

    public bool TryChoose(string sentence, out NPC nurse, out string reason)
    {
        if (TryDirectName(sentence, out nurse, out reason))
            return nurse != null;

        Choice choice = Evaluate();
        nurse = choice.nurse;
        reason = choice.label;
        return nurse != null;
    }

    // True when the sentence itself decided, including a refusal for two names
    bool TryDirectName(string sentence, out NPC nurse, out string reason)
    {
        nurse = null;
        reason = null;
        if (string.IsNullOrEmpty(sentence)) return false;

        for (int i = 0; i < nurses.Length; i++)
        {
            NPC candidate = nurses[i];
            if (candidate == null || !MentionedAsAddressee(sentence, candidate.name)) continue;
            if (nurse != null)
            {
                nurse = null;
                reason = "Name one nurse";
                return true;
            }
            nurse = candidate;
        }

        if (nurse == null) return false;
        reason = "Named: " + nurse.name;
        return true;
    }

    static bool MentionedAsAddressee(string sentence, string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        int from = 0;
        while (from <= sentence.Length - name.Length)
        {
            int at = sentence.IndexOf(name, from, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return false;
            from = at + 1;
            if (!IsWholeName(sentence, at, name.Length)) continue;
            if (IsGiveTarget(sentence, at)) continue;
            return true;
        }
        return false;
    }

    // ")" and "," are not letters, so "NPC (1)," still counts as the whole name
    static bool IsWholeName(string text, int at, int length)
    {
        return IsBoundary(text, at - 1) && IsBoundary(text, at + length);
    }

    static bool IsBoundary(string text, int index)
    {
        if (index < 0 || index >= text.Length) return true;
        return !char.IsLetterOrDigit(text[index]);
    }

    // The words just before the name are "to" or "give", so this name receives an object
    static bool IsGiveTarget(string text, int index)
    {
        int end = index;
        while (end > 0 && char.IsWhiteSpace(text[end - 1])) end--;
        return EndsWithWord(text, end, "to") || EndsWithWord(text, end, "give");
    }

    static bool EndsWithWord(string text, int end, string word)
    {
        int start = end - word.Length;
        if (start < 0) return false;
        if (string.Compare(text, start, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
            return false;
        return IsBoundary(text, start - 1);
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
