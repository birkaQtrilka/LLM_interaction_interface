using UnityEngine;
using UnityEngine.AI;

public class NPC : MonoBehaviour
{
    [field: SerializeField] public Transform RightHand { get; private set; }
    [field: SerializeField] public Transform LeftHand { get; private set; }
    [field: SerializeField] public NavMeshAgent Nav { get; private set; }
    [field: SerializeField] public GrabReceiver GrabReceiver { get; private set; }
    [field: SerializeField] public Animator Anim { get; private set; }

    public bool RightHandTaken => RightHand.childCount > 0;

    public bool LeftHandTaken => LeftHand.childCount > 0;

    public void GrabItem(Transform item, bool right)
    {
        if (right && !RightHandTaken)
        {
            item.SetParent(RightHand);
            item.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            item.GetChild(0).localPosition = Vector3.zero;
        }
        else if (!right && !LeftHandTaken)
        {
            item.SetParent(LeftHand);
            item.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            item.GetChild(0).localPosition = Vector3.zero;
        }
        else
        {
            Debug.LogWarning("Hand is full");
        }
    }

    public Transform ReleaseItem(bool right)
    {
        if (right && RightHandTaken)
        {
            var item = RightHand.GetChild(0);
            item.SetParent(null);
            return item;
        }
        else if (!right && LeftHandTaken)
        {
            var item = LeftHand.GetChild(0);
            item.SetParent(null);
            return item;
        }
        Debug.LogWarning("Hand is empty");
        return null;
    }
}
