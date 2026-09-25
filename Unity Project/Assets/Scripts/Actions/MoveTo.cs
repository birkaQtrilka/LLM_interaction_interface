// MoveToAction.cs
using UnityEngine;

public class MoveTo : IAgentAction
{
    public string Name => "moveTo";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length < 1) return "moveTo requires 1 parameter: target name";

        string name = action.parameters[0];
        Transform obj = context.GetSpot(name)?.transform
            ?? context.GetObject(name)?.transform
            ?? context.GetAgent(name)?.transform;
        if (obj == null) return $"Couldn't find spot with name {name}";

        result = MovementActions.Build(agent, obj.position, action);
        return null;
    }
}