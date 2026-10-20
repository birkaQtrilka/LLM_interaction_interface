using System;
using UnityEngine;
using UnityEngine.AI;

[SelectionBase]
public class NPC : MonoBehaviour
{
    [field: SerializeField] public Transform RightHand { get; private set; }
    [field: SerializeField] public Transform LeftHand { get; private set; }
    [field: SerializeField] public Transform Head { get; private set; }
    [field: SerializeField] public NavMeshAgent Nav { get; private set; }
    [field: SerializeField] public GrabReceiver GrabReceiver { get; private set; }
    [field: SerializeField] public Animator Anim { get; private set; }
    [Header("Movement Settings")]
    [field: SerializeField] public float WalkSpeedMult { get; private set; } = 1f;
    [field: SerializeField] public float TurnSpeed {get; private set; }= 400f;      // Degrees per second the script rotates the NPC
    [field: SerializeField] public float MaxTurnAngle {get; private set; }= 60f;    // Angle that produces maximum h (1.0 or -1.0)
    [field: SerializeField] public float FaceDistance {get; private set; }= 2f;     // Distance to item where we start facing the item
    [field: SerializeField] public float DampTime {get; private set; }= 0.15f;

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
