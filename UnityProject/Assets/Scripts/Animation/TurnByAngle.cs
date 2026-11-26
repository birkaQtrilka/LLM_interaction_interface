using UnityEngine;

public class TurnByAngle : StateMachineBehaviour
{
    [SerializeField] string angleParameter = "TurnAngle";
    [SerializeField] string speedParameter = "TurnSpeed";
    [SerializeField, Range(0f, 1f)] float rotateStart = 0f;
    [SerializeField, Range(0f, 1f)] float rotateEnd = 1f;
    [SerializeField] AnimationCurve easing = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Slow down for big turns")]
    [Tooltip("Below this absolute angle, the clip plays at normal speed")]
    [SerializeField] float slowStartAngle = 90f;
    [Tooltip("Angle at which the minimum speed is reached")]
    [SerializeField] float slowFullAngle = 180f;
    [Tooltip("Playback speed at slowFullAngle (1 = no slowdown)")]
    [SerializeField, Range(0.1f, 1f)] float minSpeed = 0.6f;

    int angleHash, speedHash;
    Transform target;
    Quaternion startRotation;
    float totalAngle;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo info, int layer)
    {
        if (angleHash == 0)
        {
            angleHash = Animator.StringToHash(angleParameter);
            speedHash = Animator.StringToHash(speedParameter);
        }

        target = animator.transform.parent;
        startRotation = target.rotation;

        totalAngle = animator.GetFloat(angleHash); // signed degrees, + = right, - = left

        float t = Mathf.InverseLerp(slowStartAngle, slowFullAngle, Mathf.Abs(totalAngle));
        animator.SetFloat(speedHash, Mathf.Lerp(1f, minSpeed, t));
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo info, int layer)
    {
        float t = Mathf.InverseLerp(rotateStart, rotateEnd, info.normalizedTime);
        float fraction = easing.Evaluate(Mathf.Clamp01(t));
        target.rotation = startRotation * Quaternion.Euler(0f, totalAngle * fraction, 0f);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo info, int layer)
    {
        target.rotation = startRotation * Quaternion.Euler(0f, totalAngle, 0f);
        animator.SetFloat(speedHash, 1f); // reset for other states
    }
}