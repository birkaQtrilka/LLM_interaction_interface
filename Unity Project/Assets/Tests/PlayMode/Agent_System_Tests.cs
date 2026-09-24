using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

public class Agent_System_Tests
{
    private AgentSystem agentSystem;
    private GameObject go;
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
        NPC agent = GameObject.Instantiate(npcPrefab);
        ctx.agent = agent;
        agentSystem.Init(llm, anim, ctx);

    }

    [TearDown]
    public void TearDown()
    {
        if (go != null)
        {
            UnityEngine.Object.Destroy(go);
        }
    }

    [UnityTest]
    public IEnumerator Spam_Prevention_Full_Turn_Test()
    {
        
        yield return agentSystem.StartCoroutine(LogSetUp(agentSystem.RunSystem("Hi!")));
    }

    [UnityTest]
    public IEnumerator Spam_Prevention_GetActions_Test()
    {
        yield return agentSystem.StartCoroutine(LogSetUp(agentSystem.GetActionsJson("Hi!", ContextQuery.GetFullContext())));
    }

    IEnumerator LogSetUp(IEnumerator call)
    {
        bool warningReceived = false;

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning &&
                condition.Contains("Still processing a previous message, wait until it's done"))
            {
                warningReceived = true;
            }
        }

        Application.logMessageReceived += OnLog;

        // First RunSystem: warning should NOT occur
        yield return agentSystem.StartCoroutine(call);

        Application.logMessageReceived -= OnLog;

        Assert.IsFalse(
            warningReceived,
            "The first call should not produce the 'still processing' warning."
        );

        // Second RunSystem: warning SHOULD occur
        LogAssert.Expect(
            LogType.Warning,
            new Regex("Still processing a previous message, wait until it's done.*")
            //, "The first call should produce the 'still processing' warning."
        );

        yield return agentSystem.StartCoroutine(call);
    }
}
