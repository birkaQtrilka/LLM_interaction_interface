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
    // Variance prompts use this repeat count, and 10 repeats make a 9-to-1 split read as about 0.11

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
        public string mode;
        public string label;
        public string[] prompts;
        public ExpectedStep[] expect;
    }

    [Serializable]
    class ExpectedStep
    {
        public string name;
        public string[] parameters;
        public int[] after;
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
    public IEnumerator Expect()
    {
        yield return Run("expect");
    }

    [UnityTest]
    [Category("PromptEval")]
    [Timeout(600000)]
    public IEnumerator Variance()
    {
        yield return Run("variance");
    }

    IEnumerator Run(string mode)
    {
        Assert.IsNotNull(system, "AgentSystem was not found in the test scene.");
        LLMBackend backend = UnityEngine.Object.FindAnyObjectByType<LLMBackend>();
        Assert.IsNotNull(backend, "LLMBackend was not found in the test scene.");

        Assert.IsNotEmpty(system.contextLibrary.agents, "The test scene has no NPC.");
        string world = system.contextLibrary.GetContext(ContextQuery.GetFullContext(), null);
        string agentName = system.contextLibrary.agents[0].name;
        PromptCaseFile file = LoadCases();
        Assert.IsNotNull(file.families, "PromptCases.json has no families.");

        bool any = false;
        var failures = new List<string>();
        var report = new StringBuilder();

        foreach (PromptFamily family in file.families)
        {
            if (family.mode != mode) continue;
            any = true;
            if (mode == "expect") yield return RunExpect(backend, world, agentName, family, report, failures);
            else yield return RunVariance(backend, world, family, report, failures);
        }

        Assert.IsTrue(any, $"No prompt families with mode {mode}.");

        string text = report.ToString();
        Debug.Log(text);
        if (failures.Count > 0)
        {
            Assert.Fail(string.Join("\n", failures) + "\n" + text);
        }
    }

    IEnumerator RunExpect(LLMBackend backend, string world, string agentName, PromptFamily family, StringBuilder report, List<string> failures)
    {
        string title = string.IsNullOrEmpty(family.label) ? "expect" : family.label;
        report.AppendLine(title);

        if (family.prompts == null || family.prompts.Length == 0)
        {
            failures.Add($"{title}: no prompts");
            yield break;
        }

        foreach (string prompt in family.prompts)
        {
            CoroutineResult<ActionsResponse> result = new();
            yield return backend.GetActions(prompt, world, result);

            if (result.Status != ContextStatus.Success)
            {
                string error = result.Error ?? "request failed";
                report.AppendLine($"  {prompt}");
                report.AppendLine($"    ERROR {error}");
                failures.Add($"{title} / {prompt}: {error}");
                continue;
            }

            string chain = FormatChain(result.Response.actions);
            string mismatch = Meaning(result.Response.actions, family.expect, agentName);
            report.AppendLine($"  {prompt}");
            report.AppendLine($"    {chain}");
            if (mismatch == null)
            {
                report.AppendLine("    ok");
            }
            else
            {
                report.AppendLine($"    {mismatch}");
                failures.Add($"{title} / {prompt}: {mismatch}");
            }
        }

        report.AppendLine();
    }

    IEnumerator RunVariance(LLMBackend backend, string world, PromptFamily family, StringBuilder report, List<string> failures)
    {
        string title = string.IsNullOrEmpty(family.label) ? "variance" : family.label;
        report.AppendLine(title);

        if (family.prompts == null || family.prompts.Length == 0)
        {
            failures.Add($"{title}: no prompts");
            yield break;
        }

        foreach (string prompt in family.prompts)
        {
            report.AppendLine($"  {prompt}");
            int trials = 0;
            var counts = new Dictionary<string, int>();

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
                string way = Behavior(result.Response.actions);
                counts[way] = counts.TryGetValue(way, out int n) ? n + 1 : 1;
                report.AppendLine($"    {i}  {FormatChain(result.Response.actions)}");
            }

            if (trials == 0)
            {
                report.AppendLine("  no successful trials");
                report.AppendLine();
                continue;
            }

            var ranked = new List<KeyValuePair<string, int>>(counts);
            ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (KeyValuePair<string, int> pair in ranked)
            {
                report.AppendLine($"    {pair.Key} × {pair.Value}");
            }

            // (trials - most common) / most common: 0 is one way, 1 is two ways split evenly
            int most = ranked[0].Value;
            float variance = most == trials ? 0f : (trials - most) / (float)most;
            report.AppendLine($"  variance {variance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
            report.AppendLine();
        }
    }

    // Match names and targets, and ignore ids, talk wording, and a comment beside the plan
    static string Meaning(ActionData[] actions, ExpectedStep[] expect, string agentName)
    {
        if (expect == null || expect.Length == 0) return "no expected actions";

        List<Step> steps = Order(actions);
        var work = new List<ActionData>();
        bool talked = false;
        foreach (Step step in steps)
        {
            if (step.action.name == "talk") talked = true;
            else work.Add(step.action);
        }

        bool talkOnly = true;
        foreach (ExpectedStep step in expect)
        {
            if (!string.Equals(step.name, "talk", StringComparison.OrdinalIgnoreCase)) talkOnly = false;
        }

        if (talkOnly)
        {
            if (work.Count > 0) return "expected only talk, got " + FormatChain(actions);
            if (!talked) return "expected talk";
            foreach (Step step in steps)
            {
                if (step.action.name == "talk" && !string.Equals(step.action.agent, agentName, StringComparison.Ordinal))
                {
                    return $"talk is assigned to {step.action.agent}";
                }
            }
            return null;
        }

        var matched = new ActionData[expect.Length];
        var used = new HashSet<int>();
        for (int i = 0; i < expect.Length; i++)
        {
            int found = -1;
            for (int p = 0; p < work.Count; p++)
            {
                if (used.Contains(p)) continue;
                if (SameStep(expect[i], work[p]))
                {
                    found = p;
                    break;
                }
            }
            if (found < 0) return "missing " + ExpectLabel(expect[i]);
            used.Add(found);
            matched[i] = work[found];
            if (!string.Equals(matched[i].agent, agentName, StringComparison.Ordinal))
            {
                return $"{matched[i].name} is assigned to {matched[i].agent}";
            }
        }

        if (used.Count != work.Count) return "extra actions: " + FormatChain(actions);

        for (int i = 0; i < expect.Length; i++)
        {
            if (expect[i].after == null) continue;
            foreach (int prev in expect[i].after)
            {
                if (prev < 0 || prev >= matched.Length) return "bad after index";
                if (!WaitsFor(matched[i], matched[prev].id))
                {
                    return $"{expect[i].name} does not wait for {expect[prev].name}";
                }
            }
        }

        return null;
    }

    static bool SameStep(ExpectedStep expect, ActionData actual)
    {
        if (!string.Equals(expect.name, actual.name, StringComparison.OrdinalIgnoreCase)) return false;
        if (expect.parameters == null || expect.parameters.Length == 0) return true;
        if (actual.parameters == null || actual.parameters.Length != expect.parameters.Length) return false;
        for (int i = 0; i < expect.parameters.Length; i++)
        {
            if (!SameToken(expect.parameters[i], actual.parameters[i])) return false;
        }
        return true;
    }

    // SpotA and "spot a" are the same target, and 1 and 1.0 are the same coordinate
    static bool SameToken(string expected, string actual)
    {
        if (string.Equals(Fold(expected), Fold(actual), StringComparison.Ordinal)) return true;
        if (float.TryParse(expected, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float e)
            && float.TryParse(actual, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float a))
        {
            return Mathf.Abs(e - a) < 0.01f;
        }
        return false;
    }

    static string Fold(string value)
    {
        if (value == null) return "";
        return value.Replace(" ", "").ToLowerInvariant();
    }

    static bool WaitsFor(ActionData action, int id)
    {
        if (action.runAfter == null) return false;
        foreach (int prior in action.runAfter)
        {
            if (prior == id) return true;
        }
        return false;
    }

    static string ExpectLabel(ExpectedStep step)
    {
        if (step.parameters == null || step.parameters.Length == 0) return step.name;
        return $"{step.name}({string.Join(", ", step.parameters)})";
    }

    // A way of acting is the action names in runAfter order
    // Parameters stay on the trial line, so two fetches of different items are one way
    // Talk next to a real plan is a comment, and talk alone is its own way
    static string Behavior(ActionData[] actions)
    {
        List<Step> steps = Order(actions);
        if (steps.Count == 0) return "(no actions)";

        bool hasWork = false;
        foreach (Step step in steps)
        {
            if (step.action.name != "talk") hasWork = true;
        }
        if (!hasWork) return "talk";

        return Join(steps, dropTalk: true, withParameters: false);
    }

    // runAfter decides order. An arrow means the step lists a runAfter. A bar means it starts on its own
    static string FormatChain(ActionData[] actions)
    {
        List<Step> steps = Order(actions);
        if (steps.Count == 0) return "(no actions)";
        return Join(steps, dropTalk: false, withParameters: true);
    }

    struct Step
    {
        public bool sequenced;
        public ActionData action;
    }

    static List<Step> Order(ActionData[] actions)
    {
        var steps = new List<Step>();
        if (actions == null || actions.Length == 0) return steps;

        var pending = new List<ActionData>(actions);
        var printed = new List<int>();

        while (pending.Count > 0)
        {
            int index = pending.FindIndex(action => DependenciesPrinted(action, printed));
            if (index < 0) index = 0;

            ActionData action = pending[index];
            pending.RemoveAt(index);
            bool sequenced = action.runAfter != null && action.runAfter.Length > 0;
            steps.Add(new Step { sequenced = sequenced, action = action });
            printed.Add(action.id);
        }

        return steps;
    }

    static string Join(List<Step> steps, bool dropTalk, bool withParameters)
    {
        var parts = new List<string>();
        foreach (Step step in steps)
        {
            if (dropTalk && step.action.name == "talk") continue;
            string piece = withParameters ? Label(step.action) : step.action.name;
            if (parts.Count == 0) parts.Add(piece);
            else if (step.sequenced) parts.Add("-> " + piece);
            else parts.Add("| " + piece);
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
