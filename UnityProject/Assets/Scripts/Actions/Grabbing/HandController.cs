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
    [Tooltip("Continuously re-applies GrabProfile offsets in LateUpdate.")]
    [SerializeField] private bool liveTweakOffset = false;

    GrabClipSwapper swapper;
    int armLayer;

    public HandState State { get; private set; }
    public GrabbableItem Held { get; private set; }
    public GrabIK IK => ik;

    void Awake()
    {
        swapper = new GrabClipSwapper(anim, grabPh, holdPh, releasePh);
        armLayer = anim.GetLayerIndex(armLayerName);
    }

    private void LateUpdate()
    {
        if (State != HandState.Holding || Held == null) return;

        if (liveTweakOffset || Held.liveTweakGrabPoint)
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
            if (item.Holder != null) item.Holder.ForceRelease();
            Attach(item);
            onGrabbed?.Invoke();
        }

        if (p.useIK) yield return ik.ReachRoutine(() => new Pos(item.GripTarget), p, contact);
        else yield return new WaitForSeconds(1.5f);

        if (Held != item) contact();
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

        if (target != null && p.useIK) yield return ik.ReachRoutine(() => new Pos(target), p, contact);
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
        tr.SetParent(holdPoint, true);
        ApplyHeldPose(item);

        anim.SetBool("Holding", true);
    }

    public void ApplyHeldPose(GrabbableItem item)
    {
        if (item == null || item.profile == null || item.GripTarget == null) return;

        GrabProfile p = item.profile;
        Transform tr = item.transform;

        Matrix4x4 offset = PoseUtil.Local(p.handOffsetPosition, p.handOffsetEuler);
        Matrix4x4 gripInItem = PoseUtil.Pose(tr).inverse * PoseUtil.Pose(item.GripTarget);
        Matrix4x4 itemPose = PoseUtil.Pose(holdPoint) * offset.inverse * gripInItem.inverse;

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
        anim.SetBool("Holding", false);
        return tr;
    }
}