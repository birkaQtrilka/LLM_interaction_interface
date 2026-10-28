using UnityEngine;

[CreateAssetMenu(menuName = "Grab/Grab Profile")]
public class GrabProfile : ScriptableObject
{
    [Header("Animation")]
    public AnimationClip grabClip;   // reach and close fingers, contains the GrabPoint event
    public AnimationClip holdClip;   // looping pose while carrying
    public AnimationClip releaseClip; // optional, can be null

    [Header("Reach IK")]
    public float reachDuration = 0.6f;
    public AnimationCurve reachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public Vector3 handRotationOffsetEuler;   // replaces your unused grabOffset
    public bool useIK = true;

    [Header("Hold")]
    public Vector3 holdLocalPosition;
    public Vector3 holdLocalEuler;
    public float ikBlendOutDuration = 0.25f;
}