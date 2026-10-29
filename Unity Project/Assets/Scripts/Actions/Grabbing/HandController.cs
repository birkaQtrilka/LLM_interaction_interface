using System;
using System.Collections;
using UnityEngine;

public enum HandState { Empty, Grabbing, Holding, Releasing }

public class HandController : MonoBehaviour
{
    [SerializeField] Animator anim;
    [SerializeField] string armLayerName = "RightArm";
    [SerializeField] GrabIK ik;
    [Tooltip("Child of the right hand bone")]
    [SerializeField] Transform holdPoint;
    [SerializeField] AnimationClip grabPh, holdPh, releasePh;

    [Header("Debug")]
    [Tooltip("If checked, continuously re-applies GrabProfile offsets in LateUpdate so you can tweak rotation/position in real time while holding.")]
    [SerializeField] private bool liveTweakOffset = false;

    private Matrix4x4 cachedGripInItem;

    GrabClipSwapper swapper;
    int armLayer;

    public HandState State { get; private set; }
    public GrabbableItem Held { get; private set; }

    void Awake()
    {
        swapper = new GrabClipSwapper(anim, grabPh, holdPh, releasePh);
        armLayer = anim.GetLayerIndex(armLayerName);
    }

    private void LateUpdate()
    {
        // Re-apply the offset every frame after Animator evaluates bones
        if (liveTweakOffset && State == HandState.Holding && Held != null)
        {
            ApplyHeldPose(Held);
        }
    }

    [ContextMenu("Log current clips")]
    public void LogClips() => swapper?.LogState(armLayer);

    public IEnumerator Grab(GrabbableItem item, Action onGrabbed = null)
    {
        if (State != HandState.Empty)
        {
            Debug.LogWarning($"Hand is busy ({State}), cannot grab {item.name}");
            yield break;
        }

        State = HandState.Grabbing;
        GrabProfile p = item.profile;
        swapper.Apply(p);
        anim.ResetTrigger("Grab");
        anim.SetTrigger("Grab");

        void contact()
        {
            if (item.Holder != null) item.Holder.ForceRelease();   // item comes from another hand
            Attach(item);
            onGrabbed?.Invoke();
        }

        if (p.useIK) yield return ik.ReachRoutine(item.GripTarget, p, contact);
        else yield return new WaitForSeconds(p.reachDuration);

        if (Held != item) contact();   // covers no IK, or a reach that bailed out early
        State = HandState.Holding;
    }

    public IEnumerator ReleaseAt(Transform target, Action<Transform> onReleased = null)
    {
        if (State != HandState.Holding || Held == null)
        {
            Debug.LogWarning("ReleaseAt called while not holding anything");
            yield break;
        }

        State = HandState.Releasing;
        GrabProfile p = Held.profile;

        void contact()
        {
            Transform tr = Detach();
            anim.ResetTrigger("Release");
            anim.SetTrigger("Release");
            onReleased?.Invoke(tr);
        }

        if (target != null && p.useIK) yield return ik.ReachRoutine(target, p, contact);
        if (Held != null) contact();

        float until = Time.time + 2f;
        while (Time.time < until && !anim.GetCurrentAnimatorStateInfo(armLayer).IsName("Empty"))
            yield return null;

        State = HandState.Empty;
    }

    public void AttachInstant(GrabbableItem item)
    {
        if (State != HandState.Empty) return;
        swapper.Apply(item.profile);
        Attach(item);
        anim.Play("Hold", armLayer, 0f);
        State = HandState.Holding;
    }

    public void ForceRelease()
    {
        Detach();

        AnimatorStateInfo s = anim.GetCurrentAnimatorStateInfo(armLayer);
        if (s.IsName("Hold") || s.IsName("Grab") || s.IsName("Release"))
            anim.Play("Empty", armLayer, 0f);

        State = HandState.Empty;
    }

    void Attach(GrabbableItem item)
    {
        Held = item;
        item.Holder = this;
        item.SetHeld(true);

        Transform tr = item.transform;
        // Cache grip relative to item once
        cachedGripInItem = PoseUtil.Pose(tr).inverse * PoseUtil.Pose(item.GripTarget);

        ApplyHeldPose(item);
        tr.SetParent(holdPoint, true);

        anim.SetBool("Holding", true);
        StartCoroutine(DebugAfterAttach(item));
    }

    void ApplyHeldPose(GrabbableItem item)
    {
        GrabProfile p = item.profile;
        if (p == null) return;

        // Fallback in case of domain reload during play mode
        if (cachedGripInItem == Matrix4x4.zero && item.GripTarget != null)
            cachedGripInItem = PoseUtil.Pose(item.transform).inverse * PoseUtil.Pose(item.GripTarget);

        Transform tr = item.transform;
        Matrix4x4 offset = PoseUtil.Local(p.handOffsetPosition, p.handOffsetEuler);
        Matrix4x4 itemPose = PoseUtil.Pose(holdPoint) * offset.inverse * cachedGripInItem.inverse;

        itemPose.ApplyTo(tr);
    }

    Transform Detach()
    {
        if (Held == null) return null;
        Transform tr = Held.transform;
        tr.SetParent(null);
        Held.SetHeld(false);
        Held.Holder = null;
        Held = null;
        cachedGripInItem = Matrix4x4.zero;
        anim.SetBool("Holding", false);
        return tr;
    }

    IEnumerator DebugAfterAttach(GrabbableItem item)
    {
        Transform grip = item.GripTarget;
        Debug.Log($"Attach frame: distance grip to holdPoint = {Vector3.Distance(grip.position, holdPoint.position):F3}, " +
                  $"holdPoint lossyScale = {holdPoint.lossyScale}, item lossyScale = {item.transform.lossyScale}, " +
                  $"grip is child of item: {grip.IsChildOf(item.transform)}");

        yield return null;
        yield return null;

        Debug.Log($"Two frames later: distance grip to holdPoint = {Vector3.Distance(grip.position, holdPoint.position):F3}");
    }
}