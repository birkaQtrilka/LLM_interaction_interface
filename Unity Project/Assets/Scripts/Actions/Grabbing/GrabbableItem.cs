using UnityEngine;

public class GrabbableItem : MonoBehaviour
{
    public GrabProfile profile;
    [SerializeField] private Transform gripTarget; // your grab point transform
    public Transform GripTarget => gripTarget != null ? gripTarget : transform;

    [Header("Debug")]
    [Tooltip("Re-applies the grip point pose every frame while held so you can tweak the grab point live in Play mode.")]
    public bool liveTweakGrabPoint = false;

    public HandController Holder { get; set; }
    public bool IsHeld { get; private set; }

    public void SetHeld(bool held) => IsHeld = held;
}