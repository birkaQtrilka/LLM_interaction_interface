using System.Collections;
using UnityEngine;

public class Give : IAgentAction
{
    public string Name => "give";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length != 1) return "give requires 1 parameter: recipient agent name";

        NPC otherAgent = context.GetAgent(action.parameters[0]);
        if (otherAgent == null) return $"Couldn't find agent with name {action.parameters[0]}";
        Transform tempTransf = new GameObject("Temp1").transform;

        Flag interchanged = new();

        void start()
        {
            agent.Anim.SetBool("Give", true);
            otherAgent.StartCoroutine(MovementActions.TurnTowards(otherAgent, agent.transform.position));
        }

        void finishHandoff()
        {
            agent.Anim.SetBool("Give", false);
            interchanged.value = true;
        }

        IEnumerator onRaise()
        {
            yield return null;
            GrabbableItem item = agent.Hand.Held;
            if (item != null)
                yield return otherAgent.StartCoroutine(otherAgent.Hand.Grab(item, finishHandoff));
            finishHandoff();   // safe to call twice, and it stops the chain from hanging if nothing was held
        }


        IEnumerator animationChain =
            Utils.MonitorAnimatorState(agent.Anim, "Give_Raise", 1)
            .OnCrEnd(onRaise())
            .OnCrEnd(Utils.MonitorFlag(interchanged))
            .OnCrEnd(Utils.MonitorAnimatorState(agent.Anim, "Give_lower", 1));

        result = new AnimAction(action, start, animationChain, null);
        return null;
    }
}