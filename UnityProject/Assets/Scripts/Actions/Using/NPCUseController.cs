using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Handle returned to the Use action. Done is set when the use finished, failed or was cancelled.</summary>
public sealed class UseSession
{
    public bool Done { get; internal set; }
    public string Error { get; internal set; }
}

/// <summary>Per-NPC playback, lock and cleanup for use interactions. Put on the NPC root.</summary>
[DisallowMultipleComponent]
public class NPCUseController : MonoBehaviour
{
    [Header("ItemUse layer slots")]
    [Tooltip("The placeholder clip (UseSlot.anim) that the 'Use' state in MetaHuman.controller references. It is overridden per use.")]
    [SerializeField] private AnimationClip _useSlotClip;
    [SerializeField] private string _useState = "Use";
    [SerializeField] private string _reachState = "UseReach";
    [SerializeField] private string _emptyState = "Empty";
    [Tooltip("Float parameter driving the Use state's Speed Multiplier. Optional.")]
    [SerializeField] private string _speedParameter = "UseSpeed";

    [Header("Safety")]
    [SerializeField, Min(0.1f)] private float _stateEnterTimeout = 1f;
    [SerializeField, Min(1f)] private float _maxDuration = 30f;

    private NPC _npc;
    private UseSession _active;
    private bool _slotOverridden;
    private GrabIK _ownedReachIK;
    private readonly List<KeyValuePair<AnimationClip, AnimationClip>> _pairs = new();

    public bool IsUsing => _active != null;

    private void Awake()
    {
        _npc = GetComponent<NPC>();
        if (_npc == null) _npc = GetComponentInParent<NPC>();
    }

    private void OnDisable()
    {
        if (_active != null) Finish(_active, "Use cancelled: NPC disabled.");
    }

    public UseSession Begin(ResolvedUseInteraction use, out string error)
    {
        error = null;
        if (_npc == null || _npc.Anim == null) { error = $"{name}: NPC or Animator missing."; return null; }
        if (_active != null) { error = $"{_npc.name} is already using something."; return null; }

        var hand = _npc.Hand;
        if (hand != null && (hand.State == HandState.Grabbing || hand.State == HandState.Releasing))
        { error = $"{_npc.name}'s hand is busy ({hand.State}); cannot use now."; return null; }

        GrabIK ik = null;
        if (use.Profile.Motion == UseMotion.Clip)
        {
            if (_useSlotClip == null) { error = $"{name}: Use Slot Clip is not assigned."; return null; }
        }
        else
        {
            ik = hand != null && hand.IK != null ? hand.IK : _npc.GetComponentInChildren<GrabIK>();
            if (ik == null) { error = $"{_npc.name} has no GrabIK."; return null; }
            if (ik.IsReaching) { error = $"{_npc.name} is already reaching; cannot use now."; return null; }
        }

        var session = new UseSession();
        _active = session;
        StartCoroutine(Run(use, session, ik));
        return session;
    }

    /// <summary>Called from AnimAction.end. Safe if the session already finished or is null.</summary>
    public void Cancel(UseSession session)
    {
        if (session != null && session == _active) Finish(session, "Use cancelled.");
    }

    private IEnumerator Run(ResolvedUseInteraction use, UseSession session, GrabIK ik)
    {
        string err = null;
        yield return RunInner(use, session, ik, e => err = e);
        Finish(session, err);
    }

    private IEnumerator RunInner(ResolvedUseInteraction use, UseSession session, GrabIK ik, Action<string> fail)
    {
        var anim = _npc.Anim;
        int layer = (int)NPC_AnimLayer.ItemUse;
        var profile = use.Profile;
        bool heldFired = false, targetFired = false, contacted = false;

        void FireHeld()
        {
            if (heldFired) return;
            heldFired = true;
            if (use.HeldInteraction != null && use.HeldObject != null && _npc.GetItem() == use.HeldObject)
                use.HeldInteraction.PlayHeldResponse(profile);
        }
        void FireTarget()
        {
            if (targetFired) return;
            targetFired = true;
            if (use.Target != null && use.TargetInteraction != null) use.TargetInteraction.PlayResponse();
        }
        void Fire(UseTiming phase)
        {
            if (session != _active) return; // late callback after cancel/finish
            if (profile.HeldItemTiming == phase) FireHeld();
            if (use.TargetInteraction != null && use.TargetInteraction.ResponseTiming == phase) FireTarget();
        }
        void OnContact()
        {
            if (contacted) return;
            contacted = true;
            Fire(UseTiming.Contact);
        }

        if (profile.Motion == UseMotion.Clip)
        {
            var overrides = NPCAnimatorOverrides.GetOrCreate(anim);
            if (overrides == null) { fail("Animator has no controller."); yield break; }

            _pairs.Clear();
            _pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(_useSlotClip, profile.NpcClip));
            overrides.ApplyOverrides(_pairs);
            _slotOverridden = true;
            if (HasFloat(anim, _speedParameter)) anim.SetFloat(_speedParameter, profile.Speed);

            int hash = Animator.StringToHash(_useState);
            anim.Play(hash, layer, 0f);

            float waited = 0f;
            while (anim.GetCurrentAnimatorStateInfo(layer).shortNameHash != hash)
            {
                waited += Time.deltaTime;
                if (waited > _stateEnterTimeout) { fail($"ItemUse layer never entered state '{_useState}'."); yield break; }
                yield return null;
            }

            Fire(UseTiming.Start);

            float elapsed = 0f;
            bool completed = false;
            while (elapsed < _maxDuration)
            {
                if (use.Target == null) { fail("Use target was destroyed during playback."); yield break; }
                if (use.HeldInteraction != null && _npc.GetItem() != use.HeldObject)
                { fail("Held item was lost during the use."); yield break; }

                var info = anim.GetCurrentAnimatorStateInfo(layer);
                if (info.shortNameHash != hash) { fail("Use animation was interrupted."); yield break; }
                if (!contacted && info.normalizedTime >= profile.ContactTime) OnContact();
                if (info.normalizedTime >= 1f) { completed = true; break; }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!completed) { fail("Use clip timed out."); yield break; }
            OnContact(); // guarantees exactly one contact even if the clip jumped past ContactTime
        }
        else // ReachIK
        {
            Transform point = use.TargetInteraction != null ? use.TargetInteraction.InteractionPoint : use.Target;
            Pos last = new Pos(point);
            Pos Evaluate()
            {
                if (point != null) last = new Pos(point); // keeps the last pose if the target disappears
                return last;
            }

            _ownedReachIK = ik;
            anim.Play(Animator.StringToHash(_reachState), layer, 0f);
            Fire(UseTiming.Start);

            bool done = false;
            StartCoroutine(Wrap(ik.ReachRoutine(Evaluate, profile.ReachProfile, OnContact, profile.ReachTimings), () => done = true));

            float elapsed = 0f;
            while (!done && elapsed < _maxDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (!done) { fail("Reach timed out."); yield break; } // Finish cancels the reach
            if (!contacted) fail("Reach ended without contact (missing dependencies or interrupted).");
        }
    }

    private static IEnumerator Wrap(IEnumerator inner, Action onDone)
    {
        yield return inner;
        onDone();
    }

    private void Finish(UseSession session, string error)
    {
        if (session != _active) return;
        _active = null;

        if (_ownedReachIK != null) { _ownedReachIK.CancelReach(); _ownedReachIK = null; } // no-op if already finished
        StopAllCoroutines(); // prevents any late contact callbacks from our own routines

        var anim = _npc != null ? _npc.Anim : null;
        if (anim != null)
        {
            int layer = (int)NPC_AnimLayer.ItemUse;
            int current = anim.GetCurrentAnimatorStateInfo(layer).shortNameHash;
            if (current == Animator.StringToHash(_useState) || current == Animator.StringToHash(_reachState))
                anim.Play(Animator.StringToHash(_emptyState), layer, 0f); // only our own layer

            if (_slotOverridden)
            {
                var overrides = NPCAnimatorOverrides.GetOrCreate(anim);
                if (overrides != null && _useSlotClip != null)
                {
                    _pairs.Clear();
                    _pairs.Add(new KeyValuePair<AnimationClip, AnimationClip>(_useSlotClip, null)); // null restores the placeholder
                    overrides.ApplyOverrides(_pairs);
                }
                if (HasFloat(anim, _speedParameter)) anim.SetFloat(_speedParameter, 1f);
            }
        }
        _slotOverridden = false;
        session.Error = error;
        session.Done = true;
    }

    private static bool HasFloat(Animator a, string p)
    {
        if (string.IsNullOrEmpty(p)) return false;
        foreach (var x in a.parameters)
            if (x.name == p && x.type == AnimatorControllerParameterType.Float) return true;
        return false;
    }
}