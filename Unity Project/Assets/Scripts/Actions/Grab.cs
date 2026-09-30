public class Grab : IAgentAction
{
    public string Name => "grab";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length < 1) return "grab requires 1 parameter: object name";

        var item = context.GetObject(action.parameters[0]);
        if (item == null) return $"Couldn't find object with name {action.parameters[0]}";
        var grabAnimator = agent.GetComponentInChildren<GrabIK>();

        void start()
        {
            agent.GrabReceiver.OnGrabPoint += snapObjectToHand;
            agent.Anim.SetTrigger("Grab");
        }

        void snapObjectToHand()
        {
            agent.GrabItem(item.transform, true);
        }

        void end()
        {
            agent.GrabReceiver.OnGrabPoint -= snapObjectToHand;
        }

        result = new AnimAction(action, start, grabAnimator.TriggerGrabRoutine(item.transform), end);
        return null;
    }
}