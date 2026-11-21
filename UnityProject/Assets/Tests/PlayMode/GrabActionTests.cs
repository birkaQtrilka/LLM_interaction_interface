using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

public class GrabActionTests
{
    AgentRig rig;
    readonly Grab grab = new();

    [UnitySetUp]
    public IEnumerator SetUp() { rig = AgentRig.Create(); yield return null; }

    [TearDown]
    public void TearDown() => rig.Dispose();

    string Build(params string[] parameters)
        => grab.TryBuild(T.Action(1, "grab", parameters: parameters), rig.System, rig.Nurse, out _);

    [Test] public void NoParameters_ReturnsError() => StringAssert.Contains("requires 1 parameter", Build());
    [Test] public void TwoParameters_ReturnsError() => StringAssert.Contains("requires 1 parameter", Build("a", "b"));
    [Test] public void EmptyName_ReturnsError() => StringAssert.Contains("non-empty", Build(""));
    [Test] public void UnknownObject_ReturnsError() => StringAssert.Contains("Couldn't find object", Build("Ghost"));

    [Test]
    public void ObjectWithoutGrabbableItem_ReturnsNotGrabbable()
    {
        var go = new GameObject("Cup");
        try
        {
            rig.Ctx.environment.Add(new ContextItem { transform = go.transform });
            StringAssert.Contains("not grabbable", Build("Cup"));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [UnityTest]
    public IEnumerator ObjectDestroyedBeforeGrabStarts_IsReportedToBackend()   // needs fix A
    {
        rig.Backend.Enqueue(T.Plan());
        yield return rig.System.RunSystem("grab the cup");     // gives the system a turn to attach feedback to

        var cup = new GameObject("Cup");
        cup.AddComponent<GrabbableItem>().profile = ScriptableObject.CreateInstance<GrabProfile>();
        rig.Ctx.environment.Add(new ContextItem { transform = cup.transform, grabbable = true });

        var action = T.Action(7, "grab", parameters: new[] { "Cup" });
        Assert.IsNull(grab.TryBuild(action, rig.System, rig.Nurse, out var built));
        Assert.IsNull(rig.Anim.ExecuteAction(built, planId: 99));
        Object.DestroyImmediate(cup);        // same frame: gone before the behavior's first real step

        yield return T.Until(() => rig.Backend.ActionCalls.Count == 2, "feedback turn");
        StringAssert.Contains("[Nurse] Cannot grab 'Cup': object no longer exists.", rig.Backend.ActionCalls[1].Prompt);
    }
}