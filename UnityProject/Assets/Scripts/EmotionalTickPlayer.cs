using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays random tick clips for the handler's current emotion on a dedicated Animator layer.
/// Ticks are cancelled when movement starts (Vertical/Horizontal above threshold, or the turn trigger is set).
/// Put next to EmotionalStateHandler.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EmotionalStateHandler), typeof(Animator))]
public class EmotionalTickPlayer : MonoBehaviour
{
    [Header("Tick layer")]
    [Tooltip("Placeholder clip that the 'Tick' state references in the controller. Overridden per tick.")]
    [SerializeField] private AnimationClip _tickSlotClip;
    [SerializeField] private string _layerName = "EmotionTick";
    [SerializeField] private string _tickState = "Tick";
    [SerializeField] private string _emptyState = "Empty";
    [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;
    [SerializeField, Min(0f)] private float _cancelFadeDuration = 0.1f;

    [Header("Movement cancel")]
    [SerializeField] private string _verticalParameter = "Vertical";
    [SerializeField] private string _horizontalParameter = "Horizontal";
    [SerializeField] private string _turnTrigger = "DoTurn";
    [SerializeField, Min(0f)] private float _movementThreshold = 0.01f;

    private static readonly List<KeyValuePair<AnimationClip, AnimationClip>> _pairs = new();

    private Animator _anim;
    private EmotionalStateHandler _handler;
    private int _layer = -1;
    private int _tickHash, _emptyHash, _vertHash, _horizHash, _turnHash;
    private bool _hasVert, _hasHoriz, _hasTurn;
    private bool _ready;
    private Coroutine _loop;
    private bool _playing;
    private bool _cancelRequested;
    private AnimationClip _appliedClip;
    private AnimationClip _lastClip;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        _handler = GetComponent<EmotionalStateHandler>();
        _tickHash = Animator.StringToHash(_tickState);
        _emptyHash = Animator.StringToHash(_emptyState);

        _layer = _anim.runtimeAnimatorController != null ? _anim.GetLayerIndex(_layerName) : -1;
        _ready = _layer >= 0 && _tickSlotClip != null;
        if (!_ready)
        {
            Debug.LogWarning($"EmotionalTickPlayer: needs layer '{_layerName}' and a Tick Slot Clip. Ticks disabled.", this);
            return;
        }

        _hasVert = HasParam(_anim, _verticalParameter, AnimatorControllerParameterType.Float);
        _hasHoriz = HasParam(_anim, _horizontalParameter, AnimatorControllerParameterType.Float);
        _hasTurn = HasParam(_anim, _turnTrigger, AnimatorControllerParameterType.Trigger)
                   || HasParam(_anim, _turnTrigger, AnimatorControllerParameterType.Bool);
        _vertHash = Animator.StringToHash(_verticalParameter ?? "");
        _horizHash = Animator.StringToHash(_horizontalParameter ?? "");
        _turnHash = Animator.StringToHash(_turnTrigger ?? "");
    }

    private void OnEnable()
    {
        _handler.OnEmotionChanged += HandleEmotionChanged;
        Restart();
    }

    // The handler applies its initial state in its own Awake, which may run after our OnEnable.
    private void Start() => Restart();

    private void OnDisable()
    {
        _handler.OnEmotionChanged -= HandleEmotionChanged;
        _loop = null; // Unity already stopped the coroutine
        Cleanup();
    }

    /// <summary>Call from your movement/turn code when it starts a turn or movement, if polling the trigger isn't reliable.</summary>
    public void CancelTick()
    {
        if (_playing) _cancelRequested = true;
    }

    private void HandleEmotionChanged(EmotionalStateSO previous, EmotionalStateSO next) => Restart();

    private void Restart()
    {
        if (!_ready) return;
        if (_playing) return; // a running tick finishes, then the loop re-reads the new state
        if (_loop != null) StopCoroutine(_loop);
        _loop = StartCoroutine(Loop());
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            var set = _handler.CurrentEmotion != null ? _handler.CurrentEmotion.Ticks : null;
            if (set == null || !set.HasClips) { _loop = null; yield break; }

            yield return WaitWhileStill(set.NextDelay());

            var clip = set.Pick(_lastClip);
            if (clip == null) continue; // WaitWhileStill always yields, so this can't spin
            _lastClip = clip;
            yield return PlayTick(clip);
        }
    }

    private IEnumerator PlayTick(AnimationClip clip)
    {
        if (clip.length <= 0f || IsMoving()) yield break;

        // The layer is in Empty here, so swapping the override is invisible.
        if (clip != _appliedClip)
        {
            var overrides = NPCAnimatorOverrides.GetOrCreate(_anim);
            if (overrides == null) yield break;
            _pairs.Clear();
            _pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(_tickSlotClip, clip));
            overrides.ApplyOverrides(_pairs);
            _appliedClip = clip;
        }

        _playing = true;
        _cancelRequested = false;
        _anim.CrossFadeInFixedTime(_tickHash, _fadeDuration, _layer, 0f);

        float returnAt = Mathf.Max(0f, clip.length - _fadeDuration);
        float elapsed = 0f;
        float fadeOut = _fadeDuration;

        while (elapsed < returnAt)
        {
            yield return null;
            elapsed += DeltaTime;
            if (_cancelRequested || IsMoving())
            {
                fadeOut = _cancelFadeDuration;
                break;
            }
        }

        _anim.CrossFadeInFixedTime(_emptyHash, fadeOut, _layer);
        yield return Wait(fadeOut);

        _cancelRequested = false;
        _playing = false;
    }

    private bool IsMoving()
    {
        if (_hasVert && Mathf.Abs(_anim.GetFloat(_vertHash)) > _movementThreshold) return true;
        if (_hasHoriz && Mathf.Abs(_anim.GetFloat(_horizHash)) > _movementThreshold) return true;
        if (_hasTurn && _anim.GetBool(_turnHash)) return true; // true while a trigger is set and not yet consumed
        return false;
    }

    // Countdown only advances while the character is still. Always yields at least one frame.
    private IEnumerator WaitWhileStill(float seconds)
    {
        float t = 0f;
        do
        {
            yield return null;
            if (!IsMoving()) t += DeltaTime;
        } while (t < seconds);
    }

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        do
        {
            yield return null;
            t += DeltaTime;
        } while (t < seconds);
    }

    private float DeltaTime => _anim.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private void Cleanup()
    {
        if (_anim == null) { _playing = false; _appliedClip = null; return; }

        if (_playing && _layer >= 0)
            _anim.Play(_emptyHash, _layer, 0f);
        _playing = false;
        _cancelRequested = false;

        if (_appliedClip != null)
        {
            var overrides = NPCAnimatorOverrides.GetOrCreate(_anim);
            if (overrides != null && _tickSlotClip != null)
            {
                _pairs.Clear();
                _pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(_tickSlotClip, null)); // restore placeholder
                overrides.ApplyOverrides(_pairs);
            }
            _appliedClip = null;
        }
    }

    private static bool HasParam(Animator a, string p, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(p)) return false;
        foreach (var x in a.parameters)
            if (x.name == p && x.type == type) return true;
        return false;
    }
}