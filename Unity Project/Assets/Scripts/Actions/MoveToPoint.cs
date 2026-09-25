using UnityEngine;

public class MoveToPoint : IAgentAction
{
    public string Name => "moveToPoint";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        var param = action.parameters;
        if (param.Length < 3) return "moveToPoint requires 3 parameters: x, y, z";

        if (!float.TryParse(param[0], out float x) ||
            !float.TryParse(param[1], out float y) ||
            !float.TryParse(param[2], out float z))
            return $"moveToPoint requires 3 numeric parameters, got: {param[0]}, {param[1]}, {param[2]}";

        result = MovementActions.Build(agent, new Vector3(x, y, z), action);
        return null;
    }
}