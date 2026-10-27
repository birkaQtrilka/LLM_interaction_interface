using System.Linq;
using UnityEngine;

public class Use : IAgentAction
{
    public string Name => "use";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        var param = action.parameters;
        if (param.Length != 1) return "requires 1 parameter: useOn (item the object in hand should be used on)";

        Transform useOn = context.GetObject(param[0])?.transform ?? context.GetAgent(param[0])?.transform;
        if (useOn == null) return $"could not find object or agent with name '{param[0]}'";
        var item = agent.GetItem(right: true);
        if (item == null) return $"agent '{agent.name}' is not holding an item in their right hand";
        ContextItem itemInHand = context.GetObject(item.name);
        if (itemInHand == null) return $"Item: '{item}' is not saved in environment objects";
        if(!itemInHand.usableOn.Contains(useOn)) return $"Item: '{item}' cannot be used on '{useOn.name}'";


        // play animation
        return null;
    }

}
