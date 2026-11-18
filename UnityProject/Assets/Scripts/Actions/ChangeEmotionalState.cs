using UnityEngine;

public class ChangeEmotionalState : IAgentAction
{
    public string Name => "changeEmotion";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        if(action.parameters.Length < 1)
        {
            result = default;
            return "changeEmotion action requires at least one parameter: the name of the emotional state to switch to.";
        }
        agent.EmotionalStateHandler.SwitchToEmotionalState(action.parameters[0]);
        // just a one shot action, no need for start, behavior or end
        result = new AnimAction
        (
            action,
            null,
            null,
            null
        );
        return null;
    }
}
