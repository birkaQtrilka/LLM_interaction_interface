using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

// Live model survey: filter the Test Runner to the PromptEval category, with the backend running
public class PromptConsistencyTests
{
    const int Repeats = 3;
    const string scenePath = "Assets/Tests/PlayMode/Scenes/Proto_1_Scene.unity";

    AgentSystem system;

    [Serializable]
    class PromptCaseFile
    {
        public PromptFamily[] families;
    }

    [Serializable]
    class PromptFamily
    {
        public string action;
        public string label;
        public string[] prompts;
    }

    [UnitySetUp]
    public IEnumerator SetupScene()
    {
#if UNITY_EDITOR
        AsyncOperation asyncLoad = EditorSceneManager.LoadSceneAsyncInPlayMode(
            scenePath,
            new LoadSceneParameters(LoadSceneMode.Additive)
        );
#else
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
#endif

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        yield return null;

        SceneManager.SetActiveScene(SceneManager.GetSceneByPath(scenePath));
        system = UnityEngine.Object.FindAnyObjectByType<AgentSystem>();
    }

    [UnityTearDown]
    public IEnumerator TearDownScene()
    {
        yield return SceneManager.UnloadSceneAsync(scenePath);
    }

    [UnityTest]
    [Category("PromptEval")]
    [Timeout(600000)]
    public IEnumerator MoveTo()
    {
        yield return Run("moveTo");
    }

    [UnityTest]
    [Category("PromptEval")]
    [Timeout(600000)]
    public IEnumerator Grab()
    {
        yield return Run("grab");
    }

    [UnityTest]
    [Category("PromptEval")]
    [Timeout(600000)]
    public IEnumerator Place()
    {
        yield return Run("place");
    }

    [UnityTest]
    [Category("PromptEval")]
    [Timeout(600000)]
    public IEnumerator Talk()
    {
        yield return Run("talk");
    }

    IEnumerator Run(string actionName)
    {
        Assert.IsNotNull(system, "AgentSystem was not found in the test scene.");
        LLMBackend backend = UnityEngine.Object.FindAnyObjectByType<LLMBackend>();
        Assert.IsNotNull(backend, "LLMBackend was not found in the test scene.");

        string world = system.contextLibrary.GetContext(ContextQuery.GetFullContext(), null);
        PromptCaseFile file = LoadCases();
        Assert.IsNotNull(file.families, "PromptCases.json has no families.");

        bool any = false;
        var failures = new List<string>();
        var report = new StringBuilder();

        foreach (PromptFamily family in file.families)
        {
            if (family.action != actionName) continue;
            any = true;
            yield return RunFamily(backend, world, family, report, failures);
        }

        Assert.IsTrue(any, $"No prompt families for {actionName}.");

        string text = report.ToString();
        Debug.Log(text);
        if (failures.Count > 0)
        {
            Assert.Fail(string.Join("\n", failures) + "\n" + text);
        }
    }

    IEnumerator RunFamily(LLMBackend backend, string world, PromptFamily family, StringBuilder report, List<string> failures)
    {
        string title = string.IsNullOrEmpty(family.label) ? family.action : family.label;
        report.AppendLine(title);

        int hits = 0;
        int trials = 0;

        if (family.prompts == null || family.prompts.Length == 0)
        {
            failures.Add($"{title}: no prompts");
            yield break;
        }

        foreach (string prompt in family.prompts)
        {
            report.AppendLine($"  {prompt}");
            for (int i = 1; i <= Repeats; i++)
            {
                CoroutineResult<ActionsResponse> result = new();
                yield return backend.GetActions(prompt, world, result);

                if (result.Status != ContextStatus.Success)
                {
                    string error = result.Error ?? "request failed";
                    report.AppendLine($"    {i}  ERROR {error}");
                    failures.Add($"{title} / {prompt} trial {i}: {error}");
                    continue;
                }

                trials++;
                if (HasAction(result.Response, family.action)) hits++;
                report.AppendLine($"    {i}  {FormatChain(result.Response.actions)}");
            }
        }

        report.AppendLine($"  {family.action} appeared in {hits}/{trials}");
        report.AppendLine();
    }

    static bool HasAction(ActionsResponse response, string actionName)
    {
        if (response?.actions == null) return false;
        foreach (ActionData action in response.actions)
        {
            if (action.name == actionName) return true;
        }
        return false;
    }

    // runAfter decides order. An arrow means the step lists a runAfter. A bar means it starts on its own
    static string FormatChain(ActionData[] actions)
    {
        if (actions == null || actions.Length == 0) return "(no actions)";

        var pending = new List<ActionData>(actions);
        var printed = new List<int>();
        var parts = new List<string>();

        while (pending.Count > 0)
        {
            int index = pending.FindIndex(action => DependenciesPrinted(action, printed));
            if (index < 0) index = 0;

            ActionData action = pending[index];
            pending.RemoveAt(index);

            bool sequenced = action.runAfter != null && action.runAfter.Length > 0;
            string piece = Label(action);
            if (parts.Count == 0) parts.Add(piece);
            else if (sequenced) parts.Add("-> " + piece);
            else parts.Add("| " + piece);

            printed.Add(action.id);
        }

        return string.Join(" ", parts);
    }

    static bool DependenciesPrinted(ActionData action, List<int> printed)
    {
        if (action.runAfter == null || action.runAfter.Length == 0) return true;
        foreach (int id in action.runAfter)
        {
            if (!printed.Contains(id)) return false;
        }
        return true;
    }

    // Spoken text changes between trials, so talk is only the action name
    static string Label(ActionData action)
    {
        if (action.name == "talk") return "talk";
        if (action.parameters == null || action.parameters.Length == 0) return action.name;
        return $"{action.name}({string.Join(", ", action.parameters)})";
    }

    static PromptCaseFile LoadCases()
    {
        string path = Path.Combine(Application.dataPath, "Tests/PlayMode/PromptCases.json");
        string json = File.ReadAllText(path);
        PromptCaseFile file = JsonUtility.FromJson<PromptCaseFile>(json);
        Assert.IsNotNull(file, "PromptCases.json did not parse.");
        return file;
    }
}
