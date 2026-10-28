using System.Collections.Generic;
using UnityEngine;

public class GrabClipSwapper
{
    readonly Animator animator;
    readonly AnimatorOverrideController overrides;
    readonly AnimationClip grabPh, holdPh, releasePh;
    readonly List<KeyValuePair<AnimationClip, AnimationClip>> list = new();

    public GrabClipSwapper(Animator animator, AnimationClip grabPh, AnimationClip holdPh, AnimationClip releasePh)
    {
        this.animator = animator;
        this.grabPh = grabPh;
        this.holdPh = holdPh;
        this.releasePh = releasePh;

        var existing = animator.runtimeAnimatorController as AnimatorOverrideController;
        RuntimeAnimatorController baseController =
            existing != null ? existing.runtimeAnimatorController : animator.runtimeAnimatorController;

        if (baseController == null)
        {
            Debug.LogError($"GrabClipSwapper: {animator.name} has no controller assigned");
            return;
        }

        Validate(baseController);

        overrides = new AnimatorOverrideController(baseController);
        if (existing != null)
        {
            var old = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            existing.GetOverrides(old);
            overrides.ApplyOverrides(old);
        }

        animator.runtimeAnimatorController = overrides;
    }

    public void Apply(GrabProfile p)
    {
        if (overrides == null) return;

        list.Clear();
        list.Add(new(grabPh, p.grabClip));
        list.Add(new(holdPh, p.holdClip));
        list.Add(new(releasePh, p.releaseClip));
        overrides.ApplyOverrides(list);
    }

    // What the controller will actually play for each placeholder right now.
    public void LogState(int layer)
    {
        Debug.Log($"Override grab: {Name(overrides[grabPh])}, hold: {Name(overrides[holdPh])}, release: {Name(overrides[releasePh])}");

        var info = animator.GetCurrentAnimatorClipInfo(layer);
        foreach (var i in info)
            Debug.Log($"Playing on layer {layer}: {i.clip.name} (weight {i.weight:0.00})");
    }

    void Validate(RuntimeAnimatorController c)
    {
        Check(c, grabPh, "grab");
        Check(c, holdPh, "hold");
        Check(c, releasePh, "release");

        if (grabPh == holdPh || grabPh == releasePh || holdPh == releasePh)
            Debug.LogError("GrabClipSwapper: the three placeholder clips must be different clips");
    }

    void Check(RuntimeAnimatorController c, AnimationClip clip, string label)
    {
        if (clip == null)
        {
            Debug.LogError($"GrabClipSwapper: the {label} placeholder is not assigned");
            return;
        }
        if (System.Array.IndexOf(c.animationClips, clip) < 0)
            Debug.LogError($"GrabClipSwapper: placeholder '{clip.name}' is not used by controller '{c.name}'. " +
                           $"Assign the exact clip that the {label} state uses.");
    }

    static string Name(AnimationClip c) => c != null ? c.name : "none";
}