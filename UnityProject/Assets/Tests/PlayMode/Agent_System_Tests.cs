using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using System;

public class Agent_System_Tests
{
    private AgentSystem agentSystem;
    private GameObject go;
    private NPC agent;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("AgentSystemTestObject");
        agentSystem = go.AddComponent<AgentSystem>();
        var llm = go.AddComponent<LLMBackend>();
        var anim = go.AddComponent<AnimationLibrary>();
        var ctx = go.AddComponent<ContextLibrary>();
        var npcPrefab = Resources.Load<NPC>("Prefabs/NPC");
        Assert.IsNotNull(npcPrefab, "There's no prefab named NPC in Resources/Prefabs");
        agent = GameObject.Instantiate(npcPrefab);
        ctx.agents.Add(agent);
        agentSystem.Init(llm, anim, ctx);

    }

    [TearDown]
    public void TearDown()
    {
        if (go != null)
            UnityEngine.Object.Destroy(go);
        if (agent != null)
            UnityEngine.Object.DestroyImmediate(agent.gameObject);
    }


    [UnityTest]
    public IEnumerator Spam_Prevention_Full_Turn_Test()
    {
        yield return agentSystem.StartCoroutine(LogSetUp(() => agentSystem.RunSystem("Hi!")) );
    }

    [UnityTest]
    public IEnumerator Spam_Prevention_GetActions_Test()
    {
        yield return agentSystem.StartCoroutine(LogSetUp(() => agentSystem.GetActionsJson("Hi!", ContextQuery.GetFullContext()) ));
    }

    IEnumerator LogSetUp(Func<IEnumerator> call)
    {
        agentSystem.StartCoroutine(call());

        // Second RunSystem: warning SHOULD occur
        LogAssert.Expect(
            LogType.Warning,
            new Regex("Still processing a previous message, wait until it's done.*")
            //, "The first call should produce the 'still processing' warning."
        );

        yield return agentSystem.StartCoroutine(call());
    }
}
