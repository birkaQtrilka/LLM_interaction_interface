using UnityEngine;

[CreateAssetMenu(fileName = "EmotionalState", menuName = "NPC/Emotional State")]
public class EmotionalStateSO : ScriptableObject
{
    [field: SerializeField, Range(-1f, 1f)] public float Arousal { get; private set; }
    [field: SerializeField, Range(-1f, 1f)] public float Valence { get; private set; }
}
