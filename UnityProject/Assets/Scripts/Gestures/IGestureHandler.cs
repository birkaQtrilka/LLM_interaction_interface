using System.Collections;
using System.Collections.Generic;

public interface IGestureHandler
{
    string Name { get; }
    IEnumerator Execute(AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args, NPC agent);
}
