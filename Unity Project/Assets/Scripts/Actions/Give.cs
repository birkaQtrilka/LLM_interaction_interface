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
        Transform tempTransf = new GameObject("Temp1").transform;

        Flag interchanged = new();
        void start()
        {
            agent.Anim.SetBool("Give", true);
        }

        IEnumerator onRaise()
        {
            yield return null;
            otherAgent.GrabReceiver.OnGrabPoint += snapObjectToHand;

            Debug.Log("Raised arm");
            Transform item = agent.GetItem(right: true);
            tempTransf.position = item.position;

            GrabIK grabAnimator = otherAgent.GetComponentInChildren<GrabIK>();
            yield return otherAgent.StartCoroutine(grabAnimator.TriggerGrabRoutine(tempTransf));
        }

        void snapObjectToHand()
        {
            Transform item = agent.ReleaseItem(right: true);
            otherAgent.GrabItem(item, right: true);
            agent.Anim.SetBool("Give", false);
            interchanged.value = true;
            otherAgent.GrabReceiver.OnGrabPoint -= snapObjectToHand;
        }

        void end()
        {
            otherAgent.GrabReceiver.OnGrabPoint -= snapObjectToHand;
            GameObject.Destroy(tempTransf.gameObject);
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