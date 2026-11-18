using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EmotionalStateHandler : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("Seconds to blend from the current emotional values to a new preset. Zero switches immediately.")]
    private float _emotionalStateTransitionDuration = 1f;
    [SerializeField, Tooltip("State applied instantly on Awake. Falls back to the first entry of Possible Emotions if empty.")]
    private EmotionalStateSO _initialEmotion;
    [SerializeField] private EmotionalStateSO[] _possibleEmotions;

    private static readonly int _arousalHash = Animator.StringToHash("Arousal");
    private static readonly int _valenceHash = Animator.StringToHash("Valence");

    private Animator _anim;
    private Coroutine _transition;
    private bool _animatorReady;

    public EmotionalStateSO[] GetEmotionalStateSOsCopy() => _possibleEmotions != null ? (EmotionalStateSO[])_possibleEmotions.Clone() : Array.Empty<EmotionalStateSO>();

    /// <summary>
    /// The active emotional state. Set at the START of a transition, so while blending
    /// this already points at the state being blended towards. Null only if the handler
    /// has no valid initial state or Animator setup.
    /// </summary>
    public EmotionalStateSO CurrentEmotion { get; private set; }

    /// <summary>True while the Animator values are still blending towards CurrentEmotion.</summary>
    public bool IsTransitioning => _transition != null;

    /// <summary>Raised when CurrentEmotion changes. Arguments are (previous, new); previous is null for the initial state.</summary>
    public event Action<EmotionalStateSO, EmotionalStateSO> OnEmotionChanged;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _animatorReady = ValidateAnimator();
        if (!_animatorReady) return;

        EmotionalStateSO initial = _initialEmotion;
        if (initial == null && _possibleEmotions != null && _possibleEmotions.Length > 0)
            initial = _possibleEmotions[0];

        if (initial == null)
        {
            Debug.LogError("EmotionalStateHandler needs an Initial Emotion or at least one entry in Possible Emotions.", this);
            return;
        }

        // Initial state is applied instantly so there is always an active state from the start.
        ApplyInstant(initial);
        EmotionalStateSO previous = CurrentEmotion;
        CurrentEmotion = initial;
        OnEmotionChanged?.Invoke(previous, initial);
    }

    public void SwitchToEmotionalState(string emotionName)
    {
        EmotionalStateSO emotionalState = null;
        if (_possibleEmotions != null && !string.IsNullOrEmpty(emotionName))
        {
            emotionalState = Array.Find(_possibleEmotions,
                e => e != null && string.Equals(e.name, emotionName, StringComparison.OrdinalIgnoreCase));
        }

        if (emotionalState == null)
        {
            Debug.LogWarning($"Cannot switch to emotional state '{emotionName}' because it was not found in the possible emotions list.", this);
            return;
        }
        SwitchToEmotionalState(emotionalState);
    }

    /// <summary>Switches to the given state. CurrentEmotion changes immediately; the Animator values blend towards it.</summary>
    public void SwitchToEmotionalState(EmotionalStateSO emotionalState)
    {
        if (emotionalState == null)
        {
            Debug.LogWarning("Cannot switch to a null emotional state.", this);
            return;
        }

        if (!_animatorReady)
        {
            Debug.LogWarning("Cannot switch emotional state: the Animator, its controller, or the Arousal/Valence float parameters are missing.", this);
            return;
        }

        // Already in (or already blending towards) this state.
        if (emotionalState == CurrentEmotion) return;

        // Cancel any blend already in progress so the new one starts from the current values.
        StopTransition();

        EmotionalStateSO previous = CurrentEmotion;
        CurrentEmotion = emotionalState;
        OnEmotionChanged?.Invoke(previous, emotionalState);

        if (_emotionalStateTransitionDuration <= 0f)
        {
            ApplyInstant(emotionalState);
            return;
        }

        _transition = StartCoroutine(BlendTo(GetTarget(emotionalState), _emotionalStateTransitionDuration));
    }

    private IEnumerator BlendTo(Vector2 target, float duration)
    {
        var start = new Vector2(_anim.GetFloat(_arousalHash), _anim.GetFloat(_valenceHash));
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return null;

            // The Animator or its controller may have been removed while we were waiting.
            if (_anim == null || _anim.runtimeAnimatorController == null)
            {
                _transition = null;
                yield break;
            }

            elapsed += _anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Vector2 values = Vector2.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            _anim.SetFloat(_arousalHash, values.x);
            _anim.SetFloat(_valenceHash, values.y);
        }

        _transition = null;
    }

    private void StopTransition()
    {
        if (_transition == null) return;
        StopCoroutine(_transition);
        _transition = null;
    }

    private void ApplyInstant(EmotionalStateSO state)
    {
        Vector2 target = GetTarget(state);
        _anim.SetFloat(_arousalHash, target.x);
        _anim.SetFloat(_valenceHash, target.y);
    }

    private static Vector2 GetTarget(EmotionalStateSO state)
    {
        return new Vector2(
            Mathf.Clamp(state.Arousal, -1f, 1f),
            Mathf.Clamp(state.Valence, -1f, 1f));
    }

    private bool ValidateAnimator()
    {
        if (_anim == null || _anim.runtimeAnimatorController == null)
        {
            Debug.LogWarning("EmotionalStateHandler requires an Animator with an assigned controller.", this);
            return false;
        }

        bool hasArousal = false;
        bool hasValence = false;
        foreach (var parameter in _anim.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Float) continue;
            if (parameter.nameHash == _arousalHash) hasArousal = true;
            if (parameter.nameHash == _valenceHash) hasValence = true;
        }

        if (!hasArousal || !hasValence)
        {
            Debug.LogWarning("EmotionalStateHandler requires Arousal and Valence float parameters on the Animator.", this);
            return false;
        }

        return true;
    }

    // Unity stops coroutines on disable. Snap to the active state so the Animator
    // never stays stuck halfway between two emotions.
    private void OnDisable()
    {
        if (_transition == null) return;
        _transition = null;
        if (_animatorReady && _anim != null && CurrentEmotion != null)
            ApplyInstant(CurrentEmotion);
    }
}