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
    public bool useIK = true;
    public float ikBlendOutDuration = 0.25f;

    [Header("Hand placement relative to the item's grip point")]
    public Vector3 handOffsetPosition;
    public Vector3 handOffsetEuler;
}

public static class PoseUtil
{
    public static Matrix4x4 Pose(Transform t) => Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
    public static Matrix4x4 Local(Vector3 pos, Vector3 euler) => Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one);
    public static void ApplyTo(this Matrix4x4 m, Transform t) => t.SetPositionAndRotation(m.GetPosition(), m.rotation);
}