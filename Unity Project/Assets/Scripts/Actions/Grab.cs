public class Grab : IAgentAction
{
    public string Name => "grab";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length < 1) return "grab requires 1 parameter: object name";

        var item = context.GetObject(action.parameters[0]);
        if (item == null) return $"Couldn't find object with name {action.parameters[0]}";
        //if (agent.GetItem(right: true) != null) return "Hand is full";

        Flag grabbed = new();
        void start()
        {
            agent.Anim.SetTrigger("Grab");
            agent.GrabReceiver.OnGrabPoint += snapObjectToHand;
        }

        void snapObjectToHand()
        {
            agent.GrabItem(item.transform, true);
            grabbed.value = true;
        }

        void end()
        {
            agent.GrabReceiver.OnGrabPoint -= snapObjectToHand;
        }

        result = new AnimAction(action, start, Utils.MonitorFlag(grabbed), end);
        return null;
    }
}