using System.Collections;
using UnityEngine;

public class GrabIKTest : MyTest
{
    [Header("Direct test")]
    [SerializeField] NPC npc;
    [SerializeField] GrabbableItem testTarget;

    [Header("Agent system test")]
    [SerializeField] string toGrab = "phone";
    [SerializeField] AnimationLibrary animationLibrary;
    [SerializeField] AgentSystem agentSystem;
    [SerializeField] float agentTestTimeout = 30f;

    Vector3 testTargetInitPos;
    Quaternion testTargetInitRot;

    void Awake()
    {
        SetTestPosition();
    }

    // Full grab through HandController: animator clips, IK reach, attach, hold.
    [ContextMenu("Grab")]
    public void TestGrab()
    {
        if (!CanRun() || !HasTarget()) return;

        ResetTestPosition();
        StartCoroutine(npc.Hand.Grab(testTarget, () => { Debug.Log($"Contact: {testTarget.name} attached"); npc.Hand.LogClips(); }));
    }

    [ContextMenu("GrabAndUse")]
    public void TestGrabAndUse()
    {
        if (!CanRun() || !HasTarget()) return;
        void release(AnimationInstance anim)
        {
            if (anim.data.name == "use")
            {
                npc.Hand.ReleaseAt(null, tr => Debug.Log($"Released {tr.name}"));
            }
            animationLibrary.AnimationStarted -= release;
        }
        animationLibrary.AnimationStarted += release;

        ResetTestPosition();
        string err = animationLibrary.PlayAnimation(agentSystem, new ActionData
        {
            id = 0,
            name = "grab",
            agent = npc.name,
            parameters = new string[] { toGrab },
        });
        if(err!=null) Debug.Log(err);
        err = animationLibrary.PlayAnimation(agentSystem, new ActionData
        {
            id = 1,
            name = "use",
            agent = npc.name,
            parameters = new string[] { npc.name },
            runAfter = new int[1] { 0 },
        });
        if(err!=null) Debug.Log(err);
    }

    // IK only: the hand reaches the grip point and blends out, nothing is attached
    // and no animator clips play. Useful for tuning rotation offsets and reach timing.
    [ContextMenu("Reach only (IK)")]
    public void TestReachOnly()
    {
        if (!CanRun() || !HasTarget()) return;

        GrabIK ik = npc.GetComponentInChildren<GrabIK>();
        if (ik == null)
        {
            Debug.LogWarning("No GrabIK found under the NPC");
            return;
        }

        ResetTestPosition();
        StartCoroutine(ik.ReachRoutine(
            testTarget.GripTarget,
            testTarget.profile,
            () => Debug.Log("Contact reached (nothing attached)")));
    }

    // Release in place: plays the release animation and drops the item where the hand is.
    [ContextMenu("Release")]
    public void TestRelease()
    {
        if (!CanRun()) return;

        StartCoroutine(npc.Hand.ReleaseAt(null, tr => Debug.Log($"Released {tr.name}")));
    }

    // Grab, wait one second, release. Checks the full cycle and that the hand returns to Empty.
    [ContextMenu("Grab then release")]
    public void TestGrabThenRelease()
    {
        if (!CanRun() || !HasTarget()) return;

        StartCoroutine(GrabThenReleaseRoutine());
    }

    IEnumerator GrabThenReleaseRoutine()
    {
        ResetTestPosition();
        yield return npc.Hand.Grab(testTarget);
        Debug.Log($"Hand state after grab: {npc.Hand.State}");
        yield return new WaitForSeconds(1f);
        yield return npc.Hand.ReleaseAt(null);
        Debug.Log($"Hand state after release: {npc.Hand.State}");
    }

    [ContextMenu("Grab via agent system")]
    public void TestGrabViaAgentSystem()
    {
        if (!CanRun()) return;
        if (animationLibrary == null || agentSystem == null)
        {
            Debug.LogWarning("Assign animationLibrary and agentSystem on the test component");
            return;
        }

        animationLibrary.PlayAnimation(agentSystem, new ActionData
        {
            id = 1,
            name = "moveTo",
            agent = npc.name,
            parameters = new string[] { toGrab },
            runAfter = new int[] { },
            delayBefore = 0
        });
        animationLibrary.PlayAnimation(agentSystem, new ActionData
        {
            id = 2,
            name = "grab",
            agent = npc.name,
            parameters = new string[] { toGrab },
            runAfter = new int[1] { 1 },
            delayBefore = 0
        });

        StartCoroutine(VerifyGrabRoutine());
    }

    IEnumerator VerifyGrabRoutine()
    {
        float until = Time.time + agentTestTimeout;
        while (Time.time < until && npc.Hand.State != HandState.Holding)
            yield return null;

        if (npc.Hand.State != HandState.Holding)
        {
            Debug.LogError($"Agent grab test FAILED: hand state is {npc.Hand.State} after {agentTestTimeout}s");
            yield break;
        }

        GrabbableItem held = npc.Hand.Held;
        bool nameMatches = held != null &&
            string.Equals(held.name, toGrab, System.StringComparison.OrdinalIgnoreCase);
        bool parented = held != null && held.transform.parent != null;

        if (nameMatches && parented)
            Debug.Log($"Agent grab test PASSED: holding {held.name}, parent {held.transform.parent.name}");
        else
            Debug.LogError($"Agent grab test FAILED: held {(held != null ? held.name : "nothing")}, expected {toGrab}, parented: {parented}");
    }


    [ContextMenu("Set test position")]
    public void SetTestPosition()
    {
        if (testTarget == null) return;
        testTargetInitPos = testTarget.transform.position;
        testTargetInitRot = testTarget.transform.rotation;
    }

    [ContextMenu("Reset test position")]
    public void ResetTestPosition()
    {
        if (testTarget == null) return;

        if (npc != null && npc.Hand != null)
        {
            if (npc.Hand.State == HandState.Grabbing || npc.Hand.State == HandState.Releasing)
            {
                Debug.LogWarning($"Hand is busy ({npc.Hand.State}), wait for it to finish before resetting");
                return;
            }
            if (npc.Hand.Held == testTarget) npc.Hand.ForceRelease();
        }

        testTarget.transform.SetParent(null);
        testTarget.transform.SetPositionAndRotation(testTargetInitPos, testTargetInitRot);

        if (testTarget.TryGetComponent(out Rigidbody rb) && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }


    bool CanRun()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Grab tests only work in play mode");
            return false;
        }
        if (npc == null || npc.Hand == null)
        {
            Debug.LogWarning("Assign an NPC with a HandController");
            return false;
        }
        return true;
    }

    bool HasTarget()
    {
        if (testTarget == null || testTarget.profile == null)
        {
            Debug.LogWarning("Assign a GrabbableItem with a GrabProfile as the test target");
            return false;
        }
        return true;
    }
}