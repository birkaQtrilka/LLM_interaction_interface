using System;
using UnityEngine;

[Serializable]
public class GrabIKSpineSettings
{
    [Tooltip("Spine bones that share the lean, ordered lowest to highest. Each needs its own MultiAimConstraint and aim target.")]
    public SpineLeanLink[] chain;
    [Range(0.5f, 1f)] public float startRatio = 0.75f;
    [Range(0.1f, 1f)] public float maxWeight = 0.85f;
    public float armLengthOffset = -0.1f;
    [Tooltip("Extra distance (meters) to overshoot the lean. Calculates reach as if the arm was shorter.")]
    [Range(0f, 0.5f)] public float overshoot = 0.1f;
    [Tooltip("Additional downward aim pitch for low reaches.")]
    [Range(0f, 45f)] public float extraDownwardPitch = 20f;
    [Tooltip("Maximum change from the unleaned spine aim direction.")]
    [Range(0f, 85f)] public float maxForwardAngle = 75f;
    [Tooltip("Shoulder motion tolerance between evaluated frames before permitting crouch.")]
    [Min(0.001f)] public float settleTolerance = 0.02f;
    [Tooltip("Distance beyond comfortable arm reach over which lean ramps to full weight.")]
    public float reachBonus = 0.3f;
    [Min(0.001f)] public float reachTolerance = 0.015f;
}
