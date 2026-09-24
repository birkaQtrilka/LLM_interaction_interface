using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class Actions_Validation_Tests
{
    AgentSystem system;
    const string scenePath = "Assets/Tests/PlayMode/Scenes/Proto_1_Scene.unity";

    [UnitySetUp]
    public IEnumerator SetupScene()
    {
        Debug.Log("Loading test scene...");

#if UNITY_EDITOR
        // Loading Additively prevents destroying the Unity Test Runner's internal scene
        AsyncOperation asyncLoad = EditorSceneManager.LoadSceneAsyncInPlayMode(
            scenePath,
            new LoadSceneParameters(LoadSceneMode.Additive)
        );
#else
        // Fallback for standalone test builds (Requires scene in Build Settings)
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
#endif

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return null;

        // Set it as active so instantiated objects go hereee
        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(scenePath));
        system = Object.FindAnyObjectByType<AgentSystem>();
    }

    [UnityTearDown]
    public IEnumerator TearDownScene()
    {
        // Clean up the additive scene so the next test starts fresh
        yield return SceneManager.UnloadSceneAsync(scenePath);
    }

    [UnityTest]
    public IEnumerator LLM_Finds_Action_From_Prompt()
    {
        string spotName = "SpotA";
        Assert.IsNotNull(system, "AgentSystem was not found in the test scene.");
        Assert.IsNotNull(system.contextLibrary.spots.Find(x => x.GetName() == spotName), "There is no object named SpotA in ContextLibrary");

        CoroutineResult<ActionsResponse> result = new();

        // Better practice: yield the enumerator directly instead of wrapping in StartCoroutine
        yield return system.GetActionsJson("Go to spot a", ContextQuery.GetFullContext(), result);

        Assert.AreEqual(result.Status, ContextStatus.Success);
        var action = result.Response.actions.FirstOrDefault(x => x.name == "moveTo");
        Assert.IsNotNull(action, "LLM did not return a moveTo action");
        Assert.AreEqual(action.parameters[0], spotName, "LLM is moving to the wrong spot");
    }

    [UnityTest]
    public IEnumerator LLM_Finds_MoveTo_Object_From_Prompt()
    {
        string objectName = "Chair";
        Assert.IsNotNull(system, "AgentSystem was not found in the test scene.");
        Assert.IsNotNull(system.contextLibrary.environment.Find(x => x.GetName() == objectName), "There is no object named Chair in ContextLibrary");
        NPC agent = system.contextLibrary.agents[0];

        CoroutineResult<ActionsResponse> result = new();
        yield return system.GetActionsJson("Go to the chair", ContextQuery.GetFullContext(), result);

        Assert.AreEqual(result.Status, ContextStatus.Success);
        ActionData action = result.Response.actions.FirstOrDefault(x => x.name == "moveTo");
        Assert.IsNotNull(action, "LLM did not return a moveTo action");
        Assert.AreEqual(agent.name, action.agent, "moveTo action is assigned to the wrong agent");
        Assert.IsNotEmpty(action.parameters, "moveTo action has no target");
        Assert.AreEqual(objectName, action.parameters[0], "LLM is moving to the wrong object");
    }

    [UnityTest]
    public IEnumerator LLM_Finds_Talk_From_Prompt()
    {
        Assert.IsNotNull(system, "AgentSystem was not found in the test scene.");
        NPC agent = system.contextLibrary.agents[0];

        CoroutineResult<ActionsResponse> result = new();
        yield return system.GetActionsJson("Say hello", ContextQuery.GetFullContext(), result);

        Assert.AreEqual(result.Status, ContextStatus.Success);
        var action = result.Response.actions.FirstOrDefault(x => x.name == "talk");
        Assert.IsNotNull(action, "LLM did not return a talk action");
        Assert.AreEqual(agent.name, action.agent, "talk action is assigned to the wrong agent");
        Assert.IsNotEmpty(action.parameters, "talk action has no message");
        Assert.That(action.parameters[0], Does.Contain("hello").IgnoreCase, "talk message does not say hello");
    }

    [UnityTest]
    public IEnumerator LLM_Finds_Grab_From_Prompt()
    {
        string objectName = "phone";
        Assert.IsNotNull(system, "AgentSystem was not found in the test scene.");
        Assert.IsNotNull(system.contextLibrary.environment.Find(x => x.GetName() == objectName), "There is no object named phone in ContextLibrary");
        NPC agent = system.contextLibrary.agents[0];

        CoroutineResult<ActionsResponse> result = new();
        yield return system.GetActionsJson("Pick up the phone", ContextQuery.GetFullContext(), result);

        Assert.AreEqual(result.Status, ContextStatus.Success);
        var move = result.Response.actions.FirstOrDefault(x => x.name == "moveTo" && x.parameters != null && x.parameters.Length > 0 && x.parameters[0] == objectName);
        var grab = result.Response.actions.FirstOrDefault(x => x.name == "grab");
        Assert.IsNull(result.Response.actions.FirstOrDefault(x => x.name == "place"), "LLM placed an item when only a grab was asked");
        Assert.IsNotNull(move, "LLM did not moveTo the phone before grabbing");
        Assert.IsNotNull(grab, "LLM did not return a grab action");
        Assert.AreEqual(agent.name, move.agent, "moveTo action is assigned to the wrong agent");
        Assert.AreEqual(agent.name, grab.agent, "grab action is assigned to the wrong agent");
        Assert.IsNotEmpty(grab.parameters, "grab action has no object name");
        Assert.AreEqual(objectName, grab.parameters[0], "LLM is grabbing the wrong object");
        Assert.That(grab.runAfter, Does.Contain(move.id), "grab does not wait for the moveTo");
    }

    // TODO: Grab reaches the hand

    [UnityTest]
    public IEnumerator Move_To_Furthest_Spot()
    {
        NPC agent = system.contextLibrary.agents[0];
        var agentStartPos = agent.transform.position;
        agentStartPos = new Vector3(agentStartPos.x, 0, agentStartPos.z);

        float dist = 0;
        Vector3 farthestPos = new();
        foreach (var p in system.contextLibrary.spots)
        {
            float currDist = Vector3.Distance(p.transform.position, agentStartPos);
            if (currDist > dist)
            {
                dist = currDist;
                Vector3 poss = p.transform.position;
                farthestPos = new Vector3(poss.x, 0, poss.z);
            }
        }
        Assert.AreNotEqual(farthestPos, new Vector3(), "farthest point is not populated");

        yield return SendAndWaitForAnimations("Move towards the farthest spot to you");

        var pos = agent.transform.position;
        pos = new Vector3(pos.x, 0, pos.z);
        Assert.That(Vector3.Distance(pos, farthestPos), Is.LessThan(0.1f), $"agent spot is {pos}, should be close to {farthestPos}");
    }

    [UnityTest]
    public IEnumerator Move_To_SpotB()
    {
        string spotName = "SpotB";
        Assert.IsNotNull(system, "AgentSystem was not found in the test scene.");
        ContextItem spot = system.contextLibrary.spots.Find(x => x.GetName() == spotName);
        Assert.IsNotNull(spot, "There is no object named SpotB in ContextLibrary");
        NPC agent = system.contextLibrary.agents[0];

        yield return SendAndWaitForAnimations("Go to SpotB");

        var pos = agent.transform.position;
        pos = new Vector3(pos.x, 0, pos.z);
        var spotPos = spot.transform.position;
        spotPos = new Vector3(spotPos.x, 0, spotPos.z);
        Assert.That(Vector3.Distance(pos, spotPos), Is.LessThan(0.1f), $"agent spot is {pos}, should be close to {spotPos}");
    }

    [UnityTest]
    public IEnumerator Place_On_Table()
    {
        yield return SendAndWaitForAnimations("place the phone and the brick on top of the table");

        ContextItem table = system.contextLibrary.environment.Find(x => x.GetName() == "Table");
        Assert.NotNull(table);
        table.FindNeighbors();

        Transform phone = table.neighbors.FirstOrDefault(x => x.name == "phone");
        Transform brick = table.neighbors.FirstOrDefault(x => x.name == "Brick");
        Assert.NotNull(phone);
        Assert.NotNull(brick);

        AssertOnTopOfBounds(brick, table.boundingBox);
        AssertOnTopOfBounds(phone, table.boundingBox);
    }

    public void AssertOnTopOfBounds(Transform tr, Bounds tableBounds)
    {
        Vector3 p = tr.position;
        Assert.GreaterOrEqual(p.y, tableBounds.max.y, $"{tr.name} center is not above the table's top surface.");
        Assert.GreaterOrEqual(p.x, tableBounds.min.x, $"{tr.name} is too far left (X min).");
        Assert.LessOrEqual(p.x, tableBounds.max.x, $"{tr.name} is too far right (X max).");
        Assert.GreaterOrEqual(p.z, tableBounds.min.z, $"{tr.name} is too far back (Z min).");
        Assert.LessOrEqual(p.z, tableBounds.max.z, $"{tr.name} is too far forward (Z max).");
    }

    public IEnumerator SendAndWaitForAnimations(string message)
    {
        CoroutineResult<ActionsResponse> result = new();
        yield return system.GetActionsJson(message, ContextQuery.GetFullContext(), result);

        yield return null;
        yield return new WaitUntil(() => system.AnimationLibrary.animations.Count == 0);
    }
}