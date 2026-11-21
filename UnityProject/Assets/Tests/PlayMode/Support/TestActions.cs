using System.Collections;
using System.Collections.Generic;

public static class TestLog
{
    public static readonly List<string> Events = new();
    public static void Clear() => Events.Clear();
}

/// <summary>"test_wait": logs start/ran/end. parameters[0] = frames to run (default 1).</summary>
public class TestWaitAction : IAgentAction
{
    public string Name => "test_wait";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        int frames = 1;
        if (action.parameters != null && action.parameters.Length > 0) int.TryParse(action.parameters[0], out frames);
        int id = (int)action.id;

        IEnumerator behavior()
        {
            TestLog.Events.Add($"ran:{id}");
            for (int i = 0; i < frames; i++) yield return null;
            TestLog.Events.Add($"done:{id}");
        }

        result = new AnimAction(action, () => TestLog.Events.Add($"start:{id}"), behavior(),
            () => TestLog.Events.Add($"end:{id}"));
        return null;
    }
}

/// <summary>"test_fail": reports an error from inside its behavior, the same way Grab does.</summary>
public class TestFailAction : IAgentAction
{
    public string Name => "test_fail";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        IEnumerator behavior()
        {
            yield return null; // same trick as Grab: make sure anim.coroutine is assigned
            context.ReportActionError("boom", agent, action);
        }
        result = new AnimAction(action, null, behavior(), null);
        return null;
    }
}