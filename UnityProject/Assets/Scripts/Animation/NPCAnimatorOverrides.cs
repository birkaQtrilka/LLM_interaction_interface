using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCAnimatorOverrides : MonoBehaviour
{
    private Animator _animator;
    private RuntimeAnimatorController _original;
    private AnimatorOverrideController _controller;
    public AnimatorOverrideController Controller => _controller;

    public static AnimatorOverrideController GetOrCreate(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return null;
        var owner = animator.GetComponent<NPCAnimatorOverrides>();
        if (owner == null) owner = animator.gameObject.AddComponent<NPCAnimatorOverrides>();
        if (owner._controller != null) return owner._controller;
        owner._animator = animator;
        owner._original = animator.runtimeAnimatorController;
        var existing = owner._original as AnimatorOverrideController;
        var baseController = existing != null ? existing.runtimeAnimatorController : owner._original;
        owner._controller = new AnimatorOverrideController(baseController);
        owner._controller.name = $"{animator.name} Runtime Overrides";
        if (existing != null)
        {
            var entries = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            existing.GetOverrides(entries);
            owner._controller.ApplyOverrides(entries);
        }
        animator.runtimeAnimatorController = owner._controller;
        return owner._controller;
    }

    private void OnDestroy()
    {
        if (_animator != null && _animator.runtimeAnimatorController == _controller)
            _animator.runtimeAnimatorController = _original;
        if (_controller != null) Destroy(_controller);
    }
}
