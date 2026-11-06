using UnityEngine;

/// <summary>
/// All timing and speed settings for GrabIK in one place.
/// Create via Assets > Create > Grab > Grab IK Timings and assign it on the GrabIK component.
/// </summary>
[CreateAssetMenu(menuName = "Grab/Grab IK Timings")]
public class GrabIKTimings : ScriptableObject
{
    [Header("Reach")]
    [Tooltip("Seconds for the hand to travel from its start pose to the grip pose.")]
    [Min(0.01f)] public float reachDuration = 0.6f;
    [Tooltip("Easing of the hand travel (x = normalized time, y = normalized progress).")]
    public AnimationCurve reachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Maximum additional settling time after the reach duration (seconds), even for unreachable targets.")]
    [Range(0f, 0.5f)] public float maxReachSettleTime = 0.2f;

    [Header("Blend Out")]
    [Tooltip("Seconds to blend the arm IK, lean, crouch and twist back out after contact.")]
    [Min(0.01f)] public float ikBlendOutDuration = 0.25f;

    [Header("Posture Speeds")]
    [Tooltip("Fraction of the reach time reserved for leaning before knees may bend.")]
    [Range(0.1f, 0.9f)] public float leanFirstFraction = 0.55f;
    [Tooltip("How quickly the chest twist weight follows lean/crouch (weight per second).")]
    [Min(0.1f)] public float twistResponseSpeed = 2f;

    [Header("Crouch")]
    [Tooltip("Seconds for the hips to drop to the required depth, measured from when crouching is allowed to start (after the lean phase). Independent of depth, so deeper crouches are not faster.")]
    [Min(0.01f)] public float crouchDuration = 0.5f;
    [Tooltip("Easing of the hips drop (x = normalized time, y = normalized progress). Ease in/out avoids an abrupt start.")]
    public AnimationCurve crouchCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
}