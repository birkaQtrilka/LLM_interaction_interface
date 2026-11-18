using UnityEngine;

public enum UseMotion { Clip, ReachIK }
public enum UseTiming { Start, Contact }

[CreateAssetMenu(menuName = "Interactions/Use Profile")]
public class UseInteractionProfile : ScriptableObject
{
    [Header("NPC Motion")]
    [SerializeField] private UseMotion _motion;
    [Tooltip("Used in Clip mode. No new NPC Animator state is needed.")]
    [SerializeField] private AnimationClip _npcClip;
    [SerializeField, Min(0.01f)] private float _speed = 1f;
    [Tooltip("Contact point within the NPC clip. IK mode uses actual reach contact instead.")]
    [SerializeField, Range(0f, 1f)] private float _contactTime = 0.5f;
    [Tooltip("Used in ReachIK mode, including existing hand offset settings.")]
    [SerializeField] private GrabProfile _reachProfile;
    [SerializeField] private GrabIKTimings _reachTimings;

    [Header("Held Item Use")]
    [Tooltip("If enabled, this item can be used on itself while held in the NPC's hand (e.g. 'use Tray' while holding Tray).")]
    [SerializeField] private bool _canUseOnSelf;
    [Tooltip("Accepted target categories. Empty means no category matches, NOT any target. Legacy usableOn references also grant permission.")]
    [SerializeField] private UseTargetCategory[] _acceptedCategories = new UseTargetCategory[0];
    [Tooltip("Required trigger on the held item's explicitly assigned Response Animator.")]
    [SerializeField] private string _heldItemTrigger = "Use";
    [SerializeField] private UseTiming _heldItemTiming = UseTiming.Start;

    public UseMotion Motion => _motion;
    public AnimationClip NpcClip => _npcClip;
    public float Speed => _speed;
    public float ContactTime => _contactTime;
    public GrabProfile ReachProfile => _reachProfile;
    public GrabIKTimings ReachTimings => _reachTimings;
    public bool CanUseOnSelf => _canUseOnSelf;
    public UseTargetCategory[] AcceptedCategories => _acceptedCategories;
    public string HeldItemTrigger => _heldItemTrigger;
    public UseTiming HeldItemTiming => _heldItemTiming;

    public string ValidateMotion()
    {
        if (_motion == UseMotion.Clip && (_npcClip == null || _npcClip.length <= 0f))
            return $"Use profile '{name}' needs a non-empty NPC clip.";
        if (_motion == UseMotion.Clip && (_speed <= 0f || float.IsNaN(_speed) || float.IsInfinity(_speed)))
            return $"Use profile '{name}' needs a positive finite speed.";
        if (_motion == UseMotion.ReachIK && (_reachProfile == null || _reachTimings == null))
            return $"Use profile '{name}' needs a reach profile and timings.";
        return null;
    }

    private void OnValidate()
    {
        if (_motion == UseMotion.Clip && _npcClip == null) Debug.LogWarning($"'{name}': Clip mode needs an NPC clip.", this);
        if (_motion == UseMotion.ReachIK && (_reachProfile == null || _reachTimings == null)) Debug.LogWarning($"'{name}': ReachIK needs profile and timings.", this);
        if (string.IsNullOrWhiteSpace(_heldItemTrigger)) Debug.LogWarning($"'{name}': held item trigger is empty.", this);
    }
}