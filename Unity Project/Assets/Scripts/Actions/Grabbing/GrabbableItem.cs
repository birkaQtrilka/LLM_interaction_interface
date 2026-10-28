using UnityEngine;

public class GrabbableItem : MonoBehaviour
{
    public GrabProfile profile;
    public Transform gripPoint;
    public Transform GripTarget => gripPoint != null ? gripPoint : transform;

    [HideInInspector] public HandController Holder;

    Rigidbody rb;

    public void SetHeld(bool held)
    {
        if (rb == null) TryGetComponent(out rb);
        if (rb != null) rb.isKinematic = held;
    }
}