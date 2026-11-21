using NUnit.Framework;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

public class Agent_System_Tests
{
    AgentRig rig;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        TestLog.Clear();
        rig = AgentRig.Create();
        yield return null; // Wait a frame for Start() (AnimationLibrary.Start starts AnimationManagerCoroutine)
    }

    [TearDown]
    public void TearDown()
    {
        rig?.Dispose();
    }

    [UnityTest]
    public IEnumerator Turn_SendsPrompt_AndPlaysReturnedActions()
    {
        rig.Backend.Enqueue(T.Plan(T.Action(1, "test_wait", parameters: new[] { "2" })));

        yield return rig.System.RunSystem("hello");
        yield return T.Until(() => TestLog.Events.Contains("done:1"), "action finished");

        Assert.AreEqual(1, rig.Backend.ActionCalls.Count);
        Assert.AreEqual("hello", rig.Backend.ActionCalls[0].Prompt);
        Assert.IsNotNull(rig.System.LastActions);
        Assert.IsFalse(rig.System.IsBusy);
    }

    [UnityTest]
    public IEnumerator Turn_WithoutSendAllContext_RunsBothRounds()
    {
        rig.System.sendAllContext = false;
        rig.Backend.Enqueue(T.Plan());

        yield return rig.System.RunSystem("hello");

        CollectionAssert.AreEqual(new[] { "hello" }, rig.Backend.ContextPrompts);
        Assert.AreEqual(1, rig.Backend.ActionCalls.Count);
        StringAssert.Contains("Nurse", rig.Backend.ActionCalls[0].World);
    }

    [UnityTest]
    public IEnumerator PlayerTurn_StoresSpeechInHistory()
    {
        rig.Backend.Enqueue(T.Plan());
        yield return rig.System.RunSystem("hello");

        StringAssert.Contains("user to everyone: hello", rig.Ctx.GetContext(new ContextQuery(), null));
    }

    [UnityTest]
    public IEnumerator HeardTurn_PassesSpeakerAndAddressee_AndSkipsPlayerHistory()
    {
        rig.Backend.Enqueue(T.Plan());
        yield return rig.System.RunHeardTurn("Where does it hurt?", "Nurse", "Doctor");

        var call = rig.Backend.ActionCalls.Single();
        Assert.AreEqual("Nurse", call.Addressee);
        Assert.AreEqual("Doctor", call.Speaker);
        StringAssert.DoesNotContain("user to", rig.Ctx.GetContext(new ContextQuery(), null));
    }

    // ---------- Busy / failures ----------

    [UnityTest]
    public IEnumerator SecondMessageWhileBusy_IsRejected()
    {
        rig.Backend.DelayFrames = 10;
        rig.Backend.Enqueue(T.Plan());
        LogAssert.Expect(LogType.Warning, new Regex("Still processing"));

        rig.System.StartCoroutine(rig.System.RunSystem("first"));
        yield return T.Frames(2);
        yield return rig.System.RunSystem("second");

        yield return T.Until(() => !rig.System.IsBusy && rig.System.LastActions != null, "first turn done");
        CollectionAssert.AreEqual(new[] { "first" }, rig.Backend.ActionCalls.Select(c => c.Prompt).ToArray());
    }

    [UnityTest]
    public IEnumerator Spam_Prevention_GetActions_Test()
    {
        rig.Backend.DelayFrames = 5;
        rig.Backend.Enqueue(T.Plan());

        var first = rig.System.StartCoroutine(rig.System.GetActionsJson("Hi!", ContextQuery.GetFullContext()));
        LogAssert.Expect(LogType.Warning, new Regex("Still processing a previous message, wait until it's done.*"));

        yield return rig.System.StartCoroutine(rig.System.GetActionsJson("Hi!", ContextQuery.GetFullContext()));
        yield return first;

        Assert.AreEqual(1, rig.Backend.ActionCalls.Count);
        Assert.IsFalse(rig.System.IsBusy);
    }

    [UnityTest]
    public IEnumerator ContextFailure_AbortsTurn_BeforeActionRound()
    {
        rig.System.sendAllContext = false;
        rig.Backend.ContextError = "network down";
        LogAssert.Expect(LogType.Error, new Regex("Error getting context"));

        yield return rig.System.RunSystem("hello");

        Assert.AreEqual(0, rig.Backend.ActionCalls.Count);
        Assert.IsFalse(rig.System.IsBusy, "busy flag must be released after failure");
    }

    [UnityTest]
    public IEnumerator ActionsFailure_IsLogged_AndBusyReleased()
    {
        rig.Backend.EnqueueError("rate limited");
        LogAssert.Expect(LogType.Error, new Regex("Error getting actions"));

        yield return rig.System.RunSystem("hello");

        Assert.IsNull(rig.System.LastActions);
        Assert.IsFalse(rig.System.IsBusy);
    }

    // ---------- Error feedback loop ----------

    [UnityTest]
    public IEnumerator UnknownAction_IsReportedBackToBackend()
    {
        rig.Backend.Enqueue(T.Plan(T.Action(1, "levitate")));
        // fallback (empty plan) answers the feedback

        yield return rig.System.RunSystem("hello");
        yield return T.Until(() => rig.Backend.ActionCalls.Count == 2, "feedback turn");

        var fb = rig.Backend.ActionCalls[1];
        StringAssert.Contains("Original request: hello", fb.Prompt);
        StringAssert.Contains("System feedback", fb.Prompt);
        StringAssert.Contains("Unknown action: levitate", fb.Prompt);
    }

    [UnityTest]
    public IEnumerator FailingAction_CancelsRestOfPlan_AndReportsIt()
    {
        rig.Backend.Enqueue(T.Plan(
            T.Action(1, "test_fail"),
            T.Action(2, "test_wait", runAfter: new[] { 1 }),                   // never started
            T.Action(3, "test_wait", parameters: new[] { "60" })));            // already running

        yield return rig.System.RunSystem("hello");
        yield return T.Until(() => rig.Backend.ActionCalls.Count == 2, "feedback turn");

        string prompt = rig.Backend.ActionCalls[1].Prompt;
        StringAssert.Contains("boom", prompt);
        StringAssert.Contains("The rest of the plan was cancelled", prompt);
        StringAssert.Contains("id 2, had not started", prompt);
        StringAssert.Contains("id 3, was running", prompt);
        CollectionAssert.DoesNotContain(TestLog.Events, "ran:2");
        CollectionAssert.DoesNotContain(TestLog.Events, "done:3");
    }

    [UnityTest]
    public IEnumerator Feedback_UsesSameContextAndAddressee_AsOriginalTurn()
    {
        rig.Backend.Enqueue(T.Plan(T.Action(1, "levitate")));

        yield return rig.System.RunHeardTurn("hi", "Nurse", "Doctor");
        yield return T.Until(() => rig.Backend.ActionCalls.Count == 2, "feedback turn");

        Assert.AreEqual("Nurse", rig.Backend.ActionCalls[1].Addressee);
        Assert.AreEqual("Doctor", rig.Backend.ActionCalls[1].Speaker);
    }

    [UnityTest]
    public IEnumerator Feedback_IsLimitedToMaxFeedbackRounds()
    {
        int nextId = 100;
        rig.Backend.Fallback = () => T.Plan(T.Action(nextId++, "test_fail"));  // always broken
        rig.System.Configure(maxFeedbackRounds: 2, errorBatchDelay: 0f);

        yield return rig.System.RunSystem("hello");
        yield return T.Until(() => rig.Backend.ActionCalls.Count == 3, "initial + 2 feedback rounds");
        yield return T.Frames(30);

        Assert.AreEqual(3, rig.Backend.ActionCalls.Count, "must not retry forever");
    }

    [UnityTest]
    public IEnumerator Feedback_TurnIsNotStoredInChatHistory()
    {
        rig.Backend.Enqueue(T.Plan(T.Action(1, "levitate")));

        yield return rig.System.RunSystem("hello");
        yield return T.Until(() => rig.Backend.ActionCalls.Count == 2, "feedback turn");
        yield return T.Frames(5);

        string ctx = rig.Ctx.GetContext(new ContextQuery(), null);
        Assert.AreEqual(1, Regex.Matches(ctx, "to everyone: hello").Count);
        StringAssert.DoesNotContain("System feedback", ctx);
    }

    [UnityTest]
    public IEnumerator IdenticalErrors_AreDeduplicated_IntoOneFeedback()
    {
        rig.Backend.Enqueue(T.Plan());
        yield return rig.System.RunSystem("hello");

        rig.System.ReportActionError("door is stuck");
        rig.System.ReportActionError("door is stuck");
        yield return T.Until(() => rig.Backend.ActionCalls.Count == 2, "feedback turn");
        yield return T.Frames(10);

        Assert.AreEqual(2, rig.Backend.ActionCalls.Count);
        Assert.AreEqual(1, Regex.Matches(rig.Backend.ActionCalls[1].Prompt, "door is stuck").Count);
    }

    [UnityTest]
    public IEnumerator Errors_BeforeAnyTurn_AreNotSent()
    {
        rig.System.ReportActionError("orphan error");
        yield return T.Frames(10);
        Assert.AreEqual(0, rig.Backend.ActionCalls.Count);
    }

    [UnityTest]
    public IEnumerator PlanArriving_SupersedesErrorsReportedWhileWaiting()
    {
        rig.Backend.DelayFrames = 5;
        rig.Backend.Enqueue(T.Plan());
        rig.System.StartCoroutine(rig.System.RunSystem("hello"));
        yield return T.Frames(2);                        // request is in flight, system is busy
        rig.System.ReportActionError("stale");           // e.g. from an action of the previous plan

        yield return T.Until(() => !rig.System.IsBusy && rig.System.LastActions != null, "turn finished");
        yield return T.Frames(15);

        Assert.AreEqual(1, rig.Backend.ActionCalls.Count, "the new plan supersedes the stale error");
    }

    [UnityTest]
    public IEnumerator BuildFailure_StopsPlanAtFirstFailedAction()
    {
        rig.Backend.Enqueue(T.Plan(
            T.Action(1, "test_wait"),
            T.Action(2, "levitate"),          // fails in TryBuild
            T.Action(3, "test_wait")));       // never queued

        yield return rig.System.RunSystem("hello");
        yield return T.Until(() => rig.Backend.ActionCalls.Count == 2, "feedback turn");

        string prompt = rig.Backend.ActionCalls[1].Prompt;
        StringAssert.Contains("Unknown action: levitate", prompt);
        StringAssert.Contains("id 1, had not started", prompt);
        CollectionAssert.DoesNotContain(TestLog.Events, "ran:1");
        CollectionAssert.DoesNotContain(TestLog.Events, "ran:3");
    }
}