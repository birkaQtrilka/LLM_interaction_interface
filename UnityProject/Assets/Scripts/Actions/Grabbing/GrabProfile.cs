using UnityEngine;

[CreateAssetMenu(menuName = "Grab/Grab Profile")]
public class GrabProfile : ScriptableObject
{
    [Header("Animation")]
    public AnimationClip grabClip;   // reach and close fingers, contains the GrabPoint event
    public AnimationClip holdClip;   // looping pose while carrying
    public AnimationClip releaseClip; // optional, can be null

    [Header("Reach IK")]
    public bool useIK = true;

    [Header("Hand placement relative to the item's grip point")]
    public Vector3 handOffsetPosition;
    public Vector3 handOffsetEuler;
}
