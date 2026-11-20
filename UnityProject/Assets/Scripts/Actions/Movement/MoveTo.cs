using UnityEngine;

public class MoveTo : IAgentAction
{
    public string Name => "moveTo";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters == null || action.parameters.Length != 1)
            return "moveTo requires 1 parameter: target name";

        string name = action.parameters[0];
        if (string.IsNullOrEmpty(name))
            return "moveTo requires a non-empty target name";

        Transform obj = context.GetSpot(name)?.transform
            ?? context.GetObject(name)?.transform
            ?? context.GetAgent(name)?.transform;
        if (obj == null) return $"Couldn't find spot, object, or agent with name '{name}'";

        result = MovementActions.Build(agent, new TransOrPos(obj), action, context);
        return null;
    }
}