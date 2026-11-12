using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Live model survey of the checkpoint judge: filter the Test Runner to the PromptEval category, with the backend running
// Do not play a scene at the same time, the backend keeps one checkpoint for the whole server
public class ScenarioJudgeTests
{
    // Each case runs this many times. Every step is one model call
    const int Repeats = 3;
    // Quick run: every case once, to see in about a minute if anything is broken
    const int QuickRepeats = 1;
    // NUnit reads this in milliseconds, so this is twenty minutes
    const int SurveyTimeoutMs = 1200000;

    GameObject backendObject;
    LLMBackend backend;

    [Serializable]
    class ScenarioCaseFile
    {
        public ScenarioCase[] cases;
    }

    [Serializable]
    class ScenarioCase
    {
        public string label;
        public ScenarioStep[] steps;
    }

    // lines are "speaker to addressee: text", the same format as the chat history
    [Serializable]
    class ScenarioStep
    {
        public string[] lines;
        public string[] actions;
        public bool advance;
    }

    [SetUp]
    public void Setup()
    {
        backendObject = new GameObject("ScenarioJudgeBackend");
        backend = backendObject.AddComponent<LLMBackend>();
    }

    [TearDown]
    public void Teardown()
    {
        UnityEngine.Object.Destroy(backendObject);
    }

    [UnityTest]
    [Category("PromptEval")]
    [Timeout(SurveyTimeoutMs)]
    public IEnumerator Judge()
    {
        yield return Run(Repeats);
    }

    [UnityTest]
    [Category("PromptQuick")]
    [Timeout(SurveyTimeoutMs)]
    public IEnumerator JudgeQuick()
    {
        yield return Run(QuickRepeats);
    }

    IEnumerator Run(int repeats)
    {
        // LLMBackend.Start opens its own session on the first frame. Let it run first, so it cannot replace the test's session
        yield return null;
        ScenarioCaseFile file = LoadCases();
        Assert.IsNotEmpty(file.cases, "ScenarioCases.json has no cases.");

        var failures = new List<string>();
        var report = new StringBuilder();
        foreach (ScenarioCase c in file.cases)
        {
            report.AppendLine(c.label);
            for (int trial = 1; trial <= repeats; trial++)
            {
                yield return RunCase(c, trial, report, failures);
            }
            report.AppendLine();
        }

        string text = report.ToString();
        Debug.Log(text);
        if (failures.Count > 0)
        {
            Assert.Fail(string.Join("\n", failures) + "\n" + text);
        }
    }

    IEnumerator RunCase(ScenarioCase c, int trial, StringBuilder report, List<string> failures)
    {
        // A new session puts the backend back at checkpoint 1
        CoroutineResult<string> session = new();
        yield return backend.StartNewSession(session);
        if (session.Status != ContextStatus.Success)
        {
            failures.Add($"{c.label} trial {trial}: session start failed: {session.Error}");
            yield break;
        }

        int expected = 1;
        // Lines pile up across steps, like the chat history in the game
        var lines = new List<string>();
        for (int s = 0; s < c.steps.Length; s++)
        {
            ScenarioStep step = c.steps[s];
            string where = $"{c.label} trial {trial} step {s + 1}";
            if (step.lines != null) lines.AddRange(step.lines);

            CoroutineResult<ScenarioCheckResponse> result = new();
            yield return backend.CheckScenario(World(lines), step.actions ?? new string[0], result);
            if (result.Status != ContextStatus.Success)
            {
                report.AppendLine($"  {trial}.{s + 1}  ERROR {result.Error}");
                failures.Add($"{where}: {result.Error}");
                yield break;
            }

            ScenarioCheckResponse r = result.Response;
            if (step.advance) expected++;
            string got = r.advanced ? "advanced" : "stayed";
            report.AppendLine($"  {trial}.{s + 1}  {got}, now {r.checkpoint} {r.name}: {r.reason}");

            // A wrong step leaves the backend at another checkpoint, so the rest of this trial would test the wrong state
            if (r.advanced != step.advance)
            {
                failures.Add($"{where}: expected {(step.advance ? "advanced" : "stayed")}, got {got}. {r.reason}");
                yield break;
            }
            if (r.checkpoint != expected)
            {
                failures.Add($"{where}: expected checkpoint {expected}, got {r.checkpoint}");
                yield break;
            }
        }
    }

    // Same header and line format as ContextLibrary.GetContext, so the judge reads what it reads in the game
    static string World(List<string> lines)
    {
        var world = new StringBuilder("\nThese are the last spoken lines, oldest first: ");
        foreach (string line in lines)
        {
            world.Append("\n- ").Append(line);
        }
        return world.ToString();
    }

    static ScenarioCaseFile LoadCases()
    {
        string path = Path.Combine(Application.dataPath, "Tests/PlayMode/ScenarioCases.json");
        string json = File.ReadAllText(path);
        ScenarioCaseFile file = JsonUtility.FromJson<ScenarioCaseFile>(json);
        Assert.IsNotNull(file, "ScenarioCases.json did not parse.");
        return file;
    }
}
