using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ContextLibraryTests
{
    GameObject root;
    ContextLibrary ctx;
    readonly List<GameObject> spawned = new();

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Ctx");
        ctx = root.AddComponent<ContextLibrary>();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var g in spawned) if (g) Object.DestroyImmediate(g);
        Object.DestroyImmediate(root);
    }

    GameObject Spawn(string name)
    {
        var go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    string Context(ContextQuery q, List<AnimationInstance> anims = null) => ctx.GetContext(q, anims);

    [Test]
    public void NoAgents_ContextSaysSo()
        => StringAssert.Contains("There are no agents in the scene", Context(new ContextQuery()));

    [Test]
    public void History_IsCappedAndOldestFirst()
    {
        ctx.maxMessageHistory = 3;
        for (int i = 1; i <= 5; i++) ctx.AddMessageToHistory("user", "Nurse", $"msg{i}");

        string s = Context(new ContextQuery());

        StringAssert.DoesNotContain("msg2", s);
        StringAssert.Contains("user to Nurse: msg3", s);
        Assert.Less(s.IndexOf("msg3"), s.IndexOf("msg5"));
    }

    [Test]
    public void Spots_OnlyIncludedWhenRequested()
    {
        ctx.spots.Add(new ContextItem { transform = Spawn("Bed").transform });

        StringAssert.DoesNotContain("Bed", Context(new ContextQuery()));
        StringAssert.Contains("name: Bed", Context(new ContextQuery { getSpots = true }));
    }

    [Test]
    public void Objects_OnlyIncludedWhenRequested_AndShowGrabbable()
    {
        ctx.environment.Add(new ContextItem { transform = Spawn("Cup").transform, grabbable = true });

        StringAssert.DoesNotContain("Cup", Context(new ContextQuery()));
        string s = Context(new ContextQuery { getObjects = true });
        StringAssert.Contains("name: Cup", s);
        StringAssert.Contains("grabbable: True", s);
    }

    [Test]
    public void PlayingAnimations_AreListedOnlyWhenPresent()
    {
        var anims = new List<AnimationInstance> { new AnimationInstance(T.Action(1, "wave"), null, null, null) };

        StringAssert.Contains("currently playing animations", Context(new ContextQuery(), anims));
        StringAssert.DoesNotContain("currently playing animations", Context(new ContextQuery(), new List<AnimationInstance>()));
    }

    [Test]
    public void DestroyedEnvironmentItem_DoesNotBreakContext()   // needs fix A
    {
        var cup = Spawn("Cup");
        ctx.environment.Add(new ContextItem { transform = cup.transform });
        ctx.environment.Add(new ContextItem { transform = Spawn("Table").transform });
        Object.DestroyImmediate(cup);

        string s = null;
        Assert.DoesNotThrow(() => s = Context(new ContextQuery { getObjects = true }));
        StringAssert.Contains("name: Table", s);
        Assert.AreEqual(1, ctx.environment.Count);
    }
}