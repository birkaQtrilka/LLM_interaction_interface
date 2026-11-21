using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class Animation_Library_Tests
{
    private GameObject root;
    private AnimationLibrary library;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        TestLog.Clear();
        root = new GameObject("AnimationLibraryTestObject");
        library = root.AddComponent<AnimationLibrary>();
        yield return null; // Start() launches the manager coroutine
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static IEnumerator Wait(int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator PushAnimation_CallsStartBehaviorAndEnd_AndRemovesWhenFinished()
    {
        var events = new List<string>();

        IEnumerator Behavior()
        {
            events.Add("behavior");
            yield return null;
        }

        var anim = library.PushAnimation(
            T.Action(1, "x"),
            Behavior(),
            () => events.Add("start"),
            () => events.Add("end"));

        yield return T.Until(() => library.animations.Count == 0, "animation finished and removed");

        Assert.IsTrue(anim.isFinished, "Animation should have finished");
        CollectionAssert.AreEqual(new[] { "start", "behavior", "end" }, events);
    }

    [UnityTest]
    public IEnumerator PushAnimation_WithNoBehavior_StillCallsStartAndEnd()
    {
        bool startCalled = false;
        bool endCalled = false;

        var anim = library.PushAnimation(
            T.Action(2, "no_behavior"),
            null,
            () => startCalled = true,
            () => endCalled = true);

        yield return T.Until(() => anim.isFinished, "no-behavior animation finished");

        Assert.IsTrue(anim.isFinished);
        Assert.IsTrue(startCalled, "start callback should run even without a behavior");
        Assert.IsTrue(endCalled, "end callback should run even without a behavior");
    }

    [UnityTest]
    public IEnumerator PushAnimation_ReturnsAnimationWithGivenData()
    {
        var action = T.Action(60, "data");
        var anim = library.PushAnimation(action, null);

        Assert.AreSame(action, anim.data, "returned Animation should reference the ActionData it was pushed with");
        Assert.IsTrue(library.animations.Contains(anim), "pushed animation should be tracked by the library");

        yield return T.Until(() => anim.isFinished, "animation finished");
    }

    [Test]
    public void PushAnimation_DuplicateId_ReturnsNull()
    {
        LogAssert.Expect(LogType.Warning, new Regex("already exists"));
        Assert.IsNotNull(library.PushAnimation(T.Action(1, "x"), Wait(1)));
        Assert.IsNull(library.PushAnimation(T.Action(1, "y"), Wait(1)));
        Assert.AreEqual(1, library.animations.Count, "Only one animation with a given id should be tracked at a time.");
    }

    [Test]
    public void ExecuteAction_DuplicateId_ReturnsErrorString()
    {
        LogAssert.Expect(LogType.Warning, new Regex("already exists"));
        library.PushAnimation(T.Action(5, "x"), Wait(1));
        string error = library.ExecuteAction(new AnimAction(T.Action(5, "y"), null, Wait(1), null));
        StringAssert.Contains("5", error);
    }

    [UnityTest]
    public IEnumerator RunAfter_DelaysDependentAnimationUntilPrerequisiteFinishes()
    {
        bool allowAToFinish = false;

        IEnumerator BehaviorA()
        {
            while (!allowAToFinish)
            {
                yield return null;
            }
        }

        var animA = library.PushAnimation(
            T.Action(10, "a"),
            BehaviorA(),
            () => TestLog.Events.Add("start1"),
            () => TestLog.Events.Add("end1"));

        library.PushAnimation(
            T.Action(20, "b", runAfter: new[] { 10 }),
            Wait(1),
            () => TestLog.Events.Add("start2"),
            null);

        // Give the scheduler a few frames to try (and correctly fail) to start B.
        yield return T.Frames(3);

        Assert.IsTrue(animA.isPlaying, "prerequisite animation should be running");
        Assert.IsFalse(TestLog.Events.Contains("start2"), "dependent animation should not start before its runAfter target finishes");

        allowAToFinish = true;

        yield return T.Until(() => TestLog.Events.Contains("start2"), "dependent started");
        Assert.Less(TestLog.Events.IndexOf("end1"), TestLog.Events.IndexOf("start2"));
    }

    [UnityTest]
    public IEnumerator RunAfter_WaitsForAllListedPrerequisites()
    {
        bool allowAToFinish = false;
        bool allowBToFinish = false;
        bool cStarted = false;

        static IEnumerator Hold(Func<bool> release)
        {
            while (!release())
            {
                yield return null;
            }
        }

        var animA = library.PushAnimation(T.Action(11, "a"), Hold(() => allowAToFinish));
        library.PushAnimation(T.Action(12, "b"), Hold(() => allowBToFinish));
        library.PushAnimation(
            T.Action(13, "c", runAfter: new[] { 11, 12 }),
            null,
            () => cStarted = true);

        yield return T.Frames(2);
        Assert.IsFalse(cStarted, "should wait while both prerequisites are still active");

        allowAToFinish = true;
        yield return T.Until(() => animA.isFinished, "prerequisite A finished");
        yield return null;
        Assert.IsFalse(cStarted, "should still wait while one prerequisite remains active");

        allowBToFinish = true;
        yield return T.Until(() => cStarted, "dependent C started");
        Assert.IsTrue(cStarted, "should start once every listed prerequisite has finished");
    }

    [UnityTest]
    public IEnumerator IndependentAnimations_PlayInParallel()
    {
        bool aRunning = false, bRunning = false;
        bool releaseA = false, releaseB = false;

        IEnumerator BehaviorA()
        {
            aRunning = true;
            while (!releaseA) yield return null;
        }

        IEnumerator BehaviorB()
        {
            bRunning = true;
            while (!releaseB) yield return null;
        }

        var animA = library.PushAnimation(T.Action(30, "a"), BehaviorA());
        var animB = library.PushAnimation(T.Action(31, "b"), BehaviorB());

        yield return T.Until(() => aRunning && bRunning, "both animations running");

        Assert.IsTrue(aRunning, "first animation should have started");
        Assert.IsTrue(bRunning, "second animation should have started concurrently");
        Assert.IsTrue(animA.isPlaying);
        Assert.IsTrue(animB.isPlaying);

        releaseA = true;
        releaseB = true;

        yield return T.Until(() => animA.isFinished && animB.isFinished, "both animations finished");

        Assert.IsTrue(animA.isFinished);
        Assert.IsTrue(animB.isFinished);
    }

    [UnityTest]
    public IEnumerator DelayBefore_PostponesStart()
    {
        float pushed = Time.time, started = -1f;
        var anim = library.PushAnimation(T.Action(40, "a", delay: 0.3f), Wait(1), () => started = Time.time, null);

        yield return null;
        Assert.Less(started, 0f, "animation should not start on the very first frame when delayBefore > 0");

        yield return T.Until(() => anim.isFinished, "delayed animation finished");
        Assert.GreaterOrEqual(started - pushed, 0.25f);
        Assert.IsTrue(anim.isFinished);
    }

    [UnityTest]
    public IEnumerator CancelPlan_StopsOnlyThatPlan_AndReportsRunningState()
    {
        var ended = new List<int>();
        library.PushAnimation(T.Action(1, "running"), Wait(100), null, () => ended.Add(1), planId: 1);
        library.PushAnimation(T.Action(2, "queued", runAfter: new[] { 1 }), Wait(100), null, () => ended.Add(2), planId: 1);
        library.PushAnimation(T.Action(3, "other"), Wait(100), null, () => ended.Add(3), planId: 2);
        yield return T.Frames(3);

        var cancelled = library.CancelPlan(1);

        Assert.AreEqual(2, cancelled.Count);
        Assert.IsTrue(cancelled.Single(c => c.anim.data.id == 1).wasRunning);
        Assert.IsFalse(cancelled.Single(c => c.anim.data.id == 2).wasRunning);
        CollectionAssert.AreEqual(new[] { 1 }, ended, "end() only runs for animations whose start() ran");
        Assert.IsFalse(library.GetAnimation(a => a.data.id == 3).isFinished);

        yield return T.Frames(2);
        Assert.IsNull(library.GetAnimation(a => a.data.id == 1), "cancelled animations are cleaned up");
    }

    [Test]
    public void CancelPlan_ZeroOrUnknownPlan_DoesNothing()
    {
        library.PushAnimation(T.Action(1, "a"), Wait(10), planId: 1);
        Assert.IsEmpty(library.CancelPlan(0));
        Assert.IsEmpty(library.CancelPlan(99));
        Assert.IsFalse(library.animations[0].isFinished);
    }

    [UnityTest]
    public IEnumerator CancelPlan_CalledTwice_ReportsNothingTheSecondTime()
    {
        library.PushAnimation(T.Action(1, "a"), Wait(50), planId: 1);
        yield return T.Frames(2);
        Assert.AreEqual(1, library.CancelPlan(1).Count);
        Assert.AreEqual(0, library.CancelPlan(1).Count);
    }

    [Test]
    public void PlayAnimation_UnknownAction_ReturnsError()
    {
        var sys = root.AddComponent<AgentSystem>();
        string err = library.PlayAnimation(sys, T.Action(1, "does_not_exist"));
        StringAssert.Contains("Unknown action", err);
    }
}