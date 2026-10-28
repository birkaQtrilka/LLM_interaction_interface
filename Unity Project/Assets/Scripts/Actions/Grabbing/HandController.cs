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

    GrabClipSwapper swapper;
    int armLayer;

    public HandState State { get; private set; }
    public GrabbableItem Held { get; private set; }
    // check if weight of ik is still on while releasing give
    // only use the hold part without going down with the arm after grab
    void Awake()
    {
        swapper = new GrabClipSwapper(anim, grabPh, holdPh, releasePh);
        armLayer = anim.GetLayerIndex(armLayerName);
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

        // Wait until the animator is back in the Empty state (with a timeout)
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
        GrabProfile p = item.profile;
        Held = item;
        item.Holder = this;
        item.SetHeld(true);
        item.transform.SetParent(holdPoint, false);
        item.transform.SetLocalPositionAndRotation(p.holdLocalPosition, Quaternion.Euler(p.holdLocalEuler));
        anim.SetBool("Holding", true);
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