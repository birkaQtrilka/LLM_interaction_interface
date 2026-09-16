using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class JSON_Respect_Test
{
    private GameObject _testObject;
    private LLMBackend _backend;

    [SetUp]
    public void Setup()
    {
        _testObject = new GameObject("TestLLMBackend");
        _backend = _testObject.AddComponent<LLMBackend>();// unnecesary
    }

    [TearDown]
    public void Teardown()
    {
        Object.Destroy(_testObject);
    }

    [UnityTest]
    public IEnumerator Backend_GetContext_ReturnsValidJSON()
    {
        bool isFinished = false;
        bool isSuccess = false;
        ContextQuery responseData = null;
        string errorMessage = "";

        _backend.GetContext(
            message: "Hello world",
            onSuccess: (response) =>
            {
                responseData = response;
                isSuccess = true;
                isFinished = true;
            },
            onError: (error) =>
            {
                errorMessage = error;
                isFinished = true;
            }
        );

        float timeout = 10f;
        while (!isFinished && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(timeout > 0f, "Test timed out waiting for backend response.");
        Assert.IsTrue(isSuccess, $"Backend request failed with error: {errorMessage}");
        Assert.IsNotNull(responseData, "Response data was null. JSON parsing failed.");
    }

    [UnityTest]
    public IEnumerator Backend_GetActions_ReturnsValidJSON()
    {
        bool isFinished = false;
        bool isSuccess = false;
        ActionsResponse responseData = null;
        string errorMessage = "";

        _backend.GetActions(
            message: "What should I do?",
            world: "TestWorld",
            onSuccess: (response) =>
            {
                responseData = response;
                isSuccess = true;
                isFinished = true;
            },
            onError: (error) =>
            {
                errorMessage = error;
                isFinished = true;
            }
        );

        // Wait for the coroutine to finish (with a 10-second timeout)
        float timeout = 10f;
        while (!isFinished && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(timeout > 0f, "Test timed out waiting for backend response.");
        Assert.IsTrue(isSuccess, $"Backend request failed with error: {errorMessage}");
        Assert.IsNotNull(responseData, "Response data was null. JSON parsing failed.");

        // Unity's JsonUtility won't throw exceptions on missing fields, it just leaves them null.
        // Therefore, we assert that the expected properties aren't null to verify the JSON structure.
        Assert.IsNotNull(responseData.actions, "The 'actions' array was missing from the JSON response.");
    }
}