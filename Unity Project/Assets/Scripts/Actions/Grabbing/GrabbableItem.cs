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

    void OnDrawGizmosSelected()
    {
        Transform g = GripTarget;
        Gizmos.color = Color.blue; Gizmos.DrawRay(g.position, g.forward * 0.08f);
        Gizmos.color = Color.green; Gizmos.DrawRay(g.position, g.up * 0.05f);
        Gizmos.color = Color.red; Gizmos.DrawRay(g.position, g.right * 0.05f);
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(g.position, 0.015f);
    }
}