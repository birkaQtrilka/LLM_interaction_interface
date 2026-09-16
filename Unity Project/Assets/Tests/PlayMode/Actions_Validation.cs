using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using System.Linq;


#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class Actions_Validation
{
    [UnitySetUp]
    public IEnumerator SetupScene()
    {
        string scenePath = "Assets/Tests/PlayMode/Scenes/Proto_1_Scene.unity";

        AsyncOperation asyncLoad = EditorSceneManager.LoadSceneAsyncInPlayMode(
            scenePath,
            new LoadSceneParameters(LoadSceneMode.Single)
        );

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator Test_Finds_Action_From_Prompt()
    {
        var system = Object.FindAnyObjectByType<AgentSystem>();
        string spotName = "SpotA";
        Assert.IsNotNull(system, "AgentSystem was not found in the test scene.");
        Assert.IsNotNull(system.contextLibrary.spots.Find(x => x.name == spotName), "There is no object named SpotA in ContextLibrary");

        CoroutineResult<ActionsResponse> result = null;
        yield return system.StartCoroutine(system.GetActionsJson("Go to spot a", ContextQuery.GetFullContext(), result));

        Assert.AreEqual(result.Status, ContextStatus.Success);
        var action = result.Response.actions.FirstOrDefault(x => x.name == "moveToSpot");
        Assert.IsNotNull(action, "LLM did not return a moveToSpot action");
        Assert.AreEqual(action.parameters[0], spotName, "LLM is moving to the wrong spot");
    }

    // TODO: add more tests for grab, check if it returns actions that don't exist etc.
}