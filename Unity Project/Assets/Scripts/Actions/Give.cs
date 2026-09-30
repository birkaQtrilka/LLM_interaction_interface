using System.Collections;
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
        Transform tempTransf = new GameObject("Temp").transform;

        Flag interchanged = new();
        void start()
        {
            agent.Anim.SetBool("Give", true);
            otherAgent.GrabReceiver.OnGrabPoint += snapObjectToHand;
        }

        IEnumerator onRaise()
        {
            Debug.Log("Raised arm");

            GrabIK grabAnimator = otherAgent.GetComponentInChildren<GrabIK>();
            return grabAnimator.TriggerGrabRoutine(tempTransf);
        }

        void snapObjectToHand()
        {
            Transform item = agent.ReleaseItem(right: true);
            otherAgent.GrabItem(item, right: true);
            agent.Anim.SetBool("Give", false);
            interchanged.value = true;
        }

        void end()
        {
            otherAgent.GrabReceiver.OnGrabPoint -= snapObjectToHand;
        }
        IEnumerator animationChain =
            Utils.MonitorAnimatorState(agent.Anim, "Give_Raise", 1)
            .OnCrEnd(onRaise())
            .OnCrEnd(Utils.MonitorFlag(interchanged))
            .OnCrEnd(Utils.MonitorAnimatorState(agent.Anim, "Give_lower", 1));

        result = new AnimAction(action, start, animationChain, end);
        return null;
    }
}