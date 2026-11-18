using UnityEngine;

[DisallowMultipleComponent]
public class UseInteractable : MonoBehaviour
{
    [Header("Interaction Profiles")]
    [Tooltip("Used when targeted without a compatible held item. None means not usable by itself.")]
    [SerializeField] private UseInteractionProfile _standaloneProfile;
    [Tooltip("Used when this object is held and the target is compatible.")]
    [SerializeField] private UseInteractionProfile _heldProfile;
    [SerializeField] private UseTargetCategory[] _categories = new UseTargetCategory[0];

    [Header("Object Response")]
    [Tooltip("Explicit object Animator. Leave empty for targets with no animated response; never automatically selects an NPC body or held prop.")]
    [SerializeField] private Animator _responseAnimator;
    [Tooltip("Optional target response trigger. Door uses UseLater, other objects may use Use.")]
    [SerializeField] private string _responseTrigger = "Use";
    [SerializeField] private UseTiming _responseTiming = UseTiming.Contact;
    [Tooltip("Optional IK interaction point. Defaults to a child GrabSpot, then the object root.")]
    [SerializeField] private Transform _interactionPoint;

    public UseInteractionProfile StandaloneProfile => _standaloneProfile;
    public UseInteractionProfile HeldProfile => _heldProfile;
    public Animator ResponseAnimator => _responseAnimator;
    public string ResponseTrigger => _responseTrigger;
    public UseTiming ResponseTiming => _responseTiming;
    public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform.FindDeep("GrabSpot") ?? transform;

    public bool HasCategory(UseTargetCategory category)
    {
        return category != null && _categories != null && System.Array.IndexOf(_categories, category) >= 0;
    }

    public static bool HasTrigger(Animator animator, string trigger)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(trigger)) return false;
        int hash = Animator.StringToHash(trigger);
        foreach (var parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }

    public void PlayResponse()
    {
        if (_responseAnimator == null || string.IsNullOrWhiteSpace(_responseTrigger)) return;
        if (!HasTrigger(_responseAnimator, _responseTrigger))
        {
            Debug.LogWarning($"Use response on '{name}' has no Trigger parameter '{_responseTrigger}'.", this);
            return;
        }
        _responseAnimator.ResetTrigger(_responseTrigger);
        _responseAnimator.SetTrigger(_responseTrigger);
    }

    public void PlayHeldResponse(UseInteractionProfile profile)
    {
        if (profile == null || !HasTrigger(_responseAnimator, profile.HeldItemTrigger)) return;
        _responseAnimator.ResetTrigger(profile.HeldItemTrigger);
        _responseAnimator.SetTrigger(profile.HeldItemTrigger);
    }
}
