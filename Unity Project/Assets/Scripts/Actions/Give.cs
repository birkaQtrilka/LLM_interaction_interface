using UnityEngine;

public static partial class Actions
{
    public static AnimAction Give(NPC agent, AgentSystem system, ActionData action)
    {
        NPC otherAgent = system.GetAgent(action.parameters[0]); 
        Debug.Assert(otherAgent != null);
        Debug.Assert(agent != null);

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

        return new AnimAction(action, start, Utils.MonitorFlag(interchanged), end);
    }

}
