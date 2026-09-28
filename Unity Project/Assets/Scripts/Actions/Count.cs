using System.Collections;
using UnityEngine;

public class Count : IAgentAction
{
    public string Name => "count";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length < 1) return "count requires 1 parameter: total";
        if (!int.TryParse(action.parameters[0], out int total)) return $"count requires a numeric parameter, got: {action.parameters[0]}";

        IEnumerator behavior()
        {
            int count = 0;
            while (count <= total)
            {
                context.chatManager?.AddChat($"{action.agent}: Count- {count++}");
                yield return new WaitForSeconds(1f);
            }
        }

        result = new AnimAction(action, null, behavior(), null);
        return null;
    }
}