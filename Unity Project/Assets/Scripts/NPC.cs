using System;
using UnityEngine;
using UnityEngine.AI;

[SelectionBase]
public class NPC : MonoBehaviour
{
    [field: SerializeField] public Transform RightHand { get; private set; }
    [field: SerializeField] public Transform LeftHand { get; private set; }
    [field: SerializeField] public NavMeshAgent Nav { get; private set; }
    [field: SerializeField] public GrabReceiver GrabReceiver { get; private set; }
    [field: SerializeField] public Animator Anim { get; private set; }
    
    public event Action<Collision> OnCollide;

    public bool RightHandTaken => RightHand.childCount > 0;

    public bool LeftHandTaken => LeftHand.childCount > 0;

    public void GrabItem(Transform item, bool right)
    {
        if (right && !RightHandTaken)
        {
            item.SetParent(RightHand);
            item.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        else if (!right && !LeftHandTaken)
        {
            item.SetParent(LeftHand);
            item.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("Hand is full");
        }
    }

    public Transform ReleaseItem(bool right)
    {
        var item = GetItem(right);
        if (item == null) return null;
        item.SetParent(null);
        return item;
    }

    public Transform GetItem(bool right)
    {
        if (right && RightHandTaken)
        {
            return RightHand.GetChild(0);
        }
        else if (!right && LeftHandTaken)
        {
            return LeftHand.GetChild(0);
        }
        return null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        OnCollide?.Invoke(collision);
    }
}
