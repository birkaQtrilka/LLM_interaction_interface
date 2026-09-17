using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
public class AnimationLibraryTests
{
    private GameObject go;
    private AnimationLibrary library;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("AnimationLibraryTestObject");
        library = go.AddComponent<AnimationLibrary>();
    }

    [TearDown]
    public void TearDown()
    {
        if (go != null)
        {
            UnityEngine.Object.Destroy(go);
        }
    }

    private static ActionData MakeAction(int id, int[] runAfter = null, float delayBefore = 0f)
    {
        return new ActionData
        {
            id = id,
            name = "test",
            parameters = Array.Empty<string>(),
            runAfter = runAfter ?? Array.Empty<int>(),
            delayBefore = delayBefore
        };
    }

    private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds = 2f)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator PushAnimation_CallsStartBehaviorAndEnd()
    {
        bool startCalled = false;
        bool behaviorRan = false;
        bool endCalled = false;

        IEnumerator Behavior()
        {
            behaviorRan = true;
            yield return null;
        }

        var anim = library.PushAnimation(
            MakeAction(1),
            Behavior(),
            () => startCalled = true,
            () => endCalled = true);

        yield return WaitUntilOrTimeout(() => anim.isFinished);

        Assert.IsTrue(anim.isFinished, "Animation should have finished within the timeout");
        Assert.IsTrue(startCalled, "start callback should have run");
        Assert.IsTrue(behaviorRan, "behavior coroutine should have run");
        Assert.IsTrue(endCalled, "end callback should have run");
    }

    [UnityTest]
    public IEnumerator PushAnimation_WithNoBehavior_StillCallsStartAndEnd()
    {
        bool startCalled = false;
        bool endCalled = false;

        var anim = library.PushAnimation(
            MakeAction(2),
            null,
            () => startCalled = true,
            () => endCalled = true);

        yield return WaitUntilOrTimeout(() => anim.isFinished);

        Assert.IsTrue(anim.isFinished);
        Assert.IsTrue(startCalled, "start callback should run even without a behavior");
        Assert.IsTrue(endCalled, "end callback should run even without a behavior");
    }

    [UnityTest]
    public IEnumerator RunAfter_DelaysDependentAnimationUntilPrerequisiteFinishes()
    {
        bool allowAToFinish = false;
        bool bStarted = false;

        IEnumerator BehaviorA()
        {
            while (!allowAToFinish)
            {
                yield return null;
            }
        }

        var animA = library.PushAnimation(MakeAction(10), BehaviorA());
        var animB = library.PushAnimation(
            MakeAction(20, runAfter: new int[] { 10 }),
            null,
            () => bStarted = true);

        // Give the scheduler a few frames to try (and correctly fail) to start B.
        yield return null;
        yield return null;
        yield return null;

        Assert.IsTrue(animA.isPlaying, "prerequisite animation should be running");
        Assert.IsFalse(bStarted, "dependent animation should not start before its runAfter target finishes");

        allowAToFinish = true;

        yield return WaitUntilOrTimeout(() => bStarted);

        Assert.IsTrue(bStarted, "dependent animation should start once its runAfter target has finished");
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

        var animA = library.PushAnimation(MakeAction(11), Hold(() => allowAToFinish));
        var animB = library.PushAnimation(MakeAction(12), Hold(() => allowBToFinish));
        library.PushAnimation(
            MakeAction(13, runAfter: new int[] { 11, 12 }),
            null,
            () => cStarted = true);

        yield return null;
        yield return null;
        Assert.IsFalse(cStarted, "should wait while both prerequisites are still active");

        allowAToFinish = true;
        yield return WaitUntilOrTimeout(() => animA.isFinished);
        yield return null;
        Assert.IsFalse(cStarted, "should still wait while one prerequisite remains active");

        allowBToFinish = true;
        yield return WaitUntilOrTimeout(() => cStarted);
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

        var animA = library.PushAnimation(MakeAction(30), BehaviorA());
        var animB = library.PushAnimation(MakeAction(31), BehaviorB());

        yield return WaitUntilOrTimeout(() => aRunning && bRunning);

        Assert.IsTrue(aRunning, "first animation should have started");
        Assert.IsTrue(bRunning, "second animation should have started concurrently");
        Assert.IsTrue(animA.isPlaying);
        Assert.IsTrue(animB.isPlaying);

        releaseA = true;
        releaseB = true;

        yield return WaitUntilOrTimeout(() => animA.isFinished && animB.isFinished);

        Assert.IsTrue(animA.isFinished);
        Assert.IsTrue(animB.isFinished);
    }

    [UnityTest]
    public IEnumerator PushAnimation_RejectsDuplicateId()
    {
        library.PushAnimation(MakeAction(99), null, () => { }, () => { });

        LogAssert.Expect(LogType.Warning, new Regex("Animation with id 99 already exists.*"));
        library.PushAnimation(MakeAction(99), null, () => { }, () => { });

        yield return null;

        int countWithId = library.animations.FindAll(a => a.data.id == 99).Count;
        Assert.AreEqual(1, countWithId, "Only one animation with a given id should be tracked at a time. ");
    }

    [UnityTest]
    public IEnumerator DelayBefore_PostponesAnimationStart()
    {
        bool started = false;
        var anim = library.PushAnimation(MakeAction(40, delayBefore: 0.3f), null, () => started = true);

        yield return null;
        Assert.IsFalse(started, "animation should not start on the very first frame when delayBefore > 0");

        yield return WaitUntilOrTimeout(() => started);

        Assert.IsTrue(started, "animation should start once delayBefore has elapsed");
        Assert.IsTrue(anim.isFinished);
    }

    [UnityTest]
    public IEnumerator FinishedAnimations_AreRemovedFromList()
    {
        var anim = library.PushAnimation(MakeAction(50), null);

        yield return WaitUntilOrTimeout(() => anim.isFinished);
        yield return null; // one more frame for the manager loop to prune it

        bool stillTracked = library.animations.Exists(a => a.data.id == 50);
        Assert.IsFalse(stillTracked, "finished animations should be removed from the animations list");
    }

    [UnityTest]
    public IEnumerator PushAnimation_ReturnsAnimationWithGivenData()
    {
        var action = MakeAction(60);
        var anim = library.PushAnimation(action, null);

        Assert.AreSame(action, anim.data, "returned Animation should reference the ActionData it was pushed with");
        Assert.IsTrue(library.animations.Contains(anim), "pushed animation should be tracked by the library");

        yield return WaitUntilOrTimeout(() => anim.isFinished);
    }
}