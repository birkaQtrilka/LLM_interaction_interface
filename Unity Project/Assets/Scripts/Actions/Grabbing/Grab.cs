public class Grab : IAgentAction
{
    public string Name => "grab";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length != 1) return "grab requires 1 parameter: object name";

        var obj = context.GetObject(action.parameters[0]);
        if (obj == null) return $"Couldn't find object with name {action.parameters[0]}";

        var item = obj.transform.GetComponent<GrabbableItem>();
        if (item == null || item.profile == null) return $"{action.parameters[0]} is not grabbable";

        result = new AnimAction(action, null, agent.Hand.Grab(item), null);
        return null;

    }
}