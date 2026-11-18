using UnityEngine;
using System.Collections.Generic;

public readonly struct ResolvedUseInteraction
{
    public readonly Transform Target;
    public readonly Transform HeldObject;
    public readonly UseInteractable TargetInteraction;
    public readonly UseInteractable HeldInteraction;
    public readonly UseInteractionProfile Profile;
    public bool UsesHeldItem => HeldInteraction != null;

    public ResolvedUseInteraction(Transform target, Transform heldObject, UseInteractable targetInteraction,
        UseInteractable heldInteraction, UseInteractionProfile profile)
    {
        Target = target;
        HeldObject = heldObject;
        TargetInteraction = targetInteraction;
        HeldInteraction = heldInteraction;
        Profile = profile;
    }
}

public static class UseInteractionResolver
{
    public static bool HasStandaloneUse(Transform target)
    {
        var interaction = target != null ? target.GetComponent<UseInteractable>() : null;
        return interaction != null && interaction.StandaloneProfile != null;
    }

    public static List<Transform> GetUsableTargets(Transform heldObject, ContextItem legacyItem)
    {
        var result = new List<Transform>();
        if (legacyItem?.usableOn != null)
            foreach (var t in legacyItem.usableOn)
                if (t != null && !result.Contains(t)) result.Add(t);

        var held = heldObject != null ? heldObject.GetComponent<UseInteractable>() : null;
        if (held == null || held.HeldProfile == null) return result;

        foreach (var candidate in Object.FindObjectsByType<UseInteractable>())
            if (!result.Contains(candidate.transform) && CanUseHeldOn(held, candidate.transform, legacyItem))
                result.Add(candidate.transform);
        return result;
    }

    public static bool CanUseHeldOn(UseInteractable held, Transform target, ContextItem legacyItem = null)
    {
        if (held == null || held.HeldProfile == null || target == null) return false;
        if (held.transform == target) return held.HeldProfile.CanUseOnSelf;
        if (legacyItem?.usableOn != null)
            foreach (var allowed in legacyItem.usableOn)
                if (allowed != null && allowed == target) return true;
        var targetInteraction = target.GetComponent<UseInteractable>();
        if (targetInteraction == null || held.HeldProfile.AcceptedCategories == null) return false;
        foreach (var category in held.HeldProfile.AcceptedCategories)
            if (targetInteraction.HasCategory(category)) return true;
        return false;
    }

    public static string Resolve(Transform target, Transform heldObject, ContextItem legacyItem, out ResolvedUseInteraction interaction)
    {
        interaction = default;
        if (target == null) return "Use target no longer exists.";
        var targetInteraction = target.GetComponent<UseInteractable>();
        var heldInteraction = heldObject != null ? heldObject.GetComponent<UseInteractable>() : null;
        if (CanUseHeldOn(heldInteraction, target, legacyItem))
        {
            string error = heldInteraction.HeldProfile.ValidateMotion();
            if (error != null) return error;
            if (!UseInteractable.HasTrigger(heldInteraction.ResponseAnimator, heldInteraction.HeldProfile.HeldItemTrigger))
                return $"Held item '{heldObject.name}' needs a Response Animator with Trigger '{heldInteraction.HeldProfile.HeldItemTrigger}'.";
            bool isSelfUse = target == heldObject;
            interaction = new ResolvedUseInteraction(
                target,
                heldObject,
                isSelfUse ? null : targetInteraction,
                heldInteraction,
                heldInteraction.HeldProfile);
            return null;
        }
        if (targetInteraction != null && targetInteraction.StandaloneProfile != null)
        {
            string error = targetInteraction.StandaloneProfile.ValidateMotion();
            if (error != null) return error;
            interaction = new ResolvedUseInteraction(target, heldObject, targetInteraction, null, targetInteraction.StandaloneProfile);
            return null;
        }
        return heldObject != null
            ? $"'{heldObject.name}' cannot be used on '{target.name}', and the target has no standalone use profile."
            : $"'{target.name}' has no standalone use profile.";
    }
}