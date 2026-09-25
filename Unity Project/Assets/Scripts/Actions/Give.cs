using UnityEngine;

public class Give : IAgentAction
{
    public string Name => "give";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length < 1) return "give requires 1 parameter: recipient agent name";

        NPC otherAgent = context.GetAgent(action.parameters[0]);
        if (otherAgent == null) return $"Couldn't find agent with name {action.parameters[0]}";
        //if (agent.GetItem(right: true) == null) return "Hand is empty";

        Flag interchanged = new();
        void start()
        {
            agent.Anim.SetTrigger("Grab"); // TODO: replace with give animation
            otherAgent.Anim.SetTrigger("Grab"); // TODO: replace with receive animation
            agent.GrabReceiver.OnGrabPoint += snapObjectToHand;
        }

        void snapObjectToHand()
        {
            Transform item = agent.ReleaseItem(right: true);
            otherAgent.GrabItem(item, right: true);
            interchanged.value = true;
        }

        void end()
        {
            agent.GrabReceiver.OnGrabPoint -= snapObjectToHand;
        }

        result = new AnimAction(action, start, Utils.MonitorFlag(interchanged), end);
        return null;
    }
}