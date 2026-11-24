using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EmotionalState", menuName = "NPC/Emotional State")]
public class EmotionalStateSO : ScriptableObject
{
    [field: SerializeField, Range(-1f, 1f)] public float Arousal { get; private set; }
    [field: SerializeField, Range(-1f, 1f)] public float Valence { get; private set; }
    [field: SerializeField] public EmotionalTick Ticks { get; private set; }
}

[Serializable]
public class EmotionalTick
{
    [Tooltip("Min and max seconds between the end of one tick clip and the start of the next.")]
    [field: SerializeField] public Vector2 Frequency { get; private set; } = new Vector2(4f, 10f);

    [Tooltip("Non-looping clips, one is picked at random each tick.")]
    [field: SerializeField] public AnimationClip[] Clips { get; private set; }

    public bool HasClips
    {
        get
        {
            if (Clips == null) return false;
            foreach (var c in Clips) if (c != null) return true;
            return false;
        }
    }

    public float NextDelay()
    {
        float min = Mathf.Max(0f, Mathf.Min(Frequency.x, Frequency.y));
        float max = Mathf.Max(0f, Mathf.Max(Frequency.x, Frequency.y));
        return UnityEngine.Random.Range(min, max);
    }

    /// <summary>Random clip, avoiding an immediate repeat of <paramref name="last"/> when there is a choice.</summary>
    public AnimationClip Pick(AnimationClip last)
    {
        if (Clips == null || Clips.Length == 0) return null;
        for (int i = 0; i < 4; i++)
        {
            var c = Clips[UnityEngine.Random.Range(0, Clips.Length)];
            if (c != null && (c != last || Clips.Length == 1)) return c;
        }
        foreach (var c in Clips) if (c != null) return c;
        return null;
    }
}