using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[Serializable]
public class GrabIKTwistSettings
{
    [Tooltip("Multi-Aim Constraint on a chest bone ABOVE the lean bones.")]
    public MultiAimConstraint chestConstraint;
    public Transform chestTarget;
    [Tooltip("Twist weight at full lean/crouch. Keep low for a gentle turn.")]
    [Range(0f, 1f)] public float maxWeight = 0.5f;
}
