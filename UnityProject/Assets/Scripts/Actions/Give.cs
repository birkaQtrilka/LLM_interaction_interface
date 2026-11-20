using System;
using System.Collections;
using UnityEngine;

public class Give : IAgentAction
{
    public string Name => "give";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters == null || action.parameters.Length != 1)
            return "give requires 1 parameter: recipient agent name";

        string recipientName = action.parameters[0];
        if (string.IsNullOrEmpty(recipientName))
            return "give requires a non-empty recipient agent name";

        NPC otherAgent = context.GetAgent(recipientName);
        if (otherAgent == null) return $"Couldn't find agent with name '{recipientName}'";
        if (otherAgent == agent) return $"'{agent.name}' cannot give an item to themselves.";

        if (agent.Hand == null)
            return $"Misconfigured: '{agent.name}' has no HandController.";
        if (otherAgent.Hand == null)
            return $"Misconfigured: '{otherAgent.name}' has no HandController.";

        Flag interchanged = new();
        string error = null;

        void start()
        {
            if (otherAgent == null)
            {
                error = $"Cannot give item to '{recipientName}': recipient agent no longer exists.";
                return;
            }

            if (agent.Hand.State != HandState.Holding || agent.Hand.Held == null)
            {
                error = $"Cannot give item to '{recipientName}': '{agent.name}' is not holding anything.";
                return;
            }

            if (otherAgent.Hand.State != HandState.Empty)
            {
                string otherHolding = otherAgent.Hand.Held != null ? $" (currently holding {otherAgent.Hand.Held.name})" : "";
                error = $"Cannot give item to '{recipientName}': recipient hand is busy ({otherAgent.Hand.State}{otherHolding}).";
                return;
            }

            agent.Anim.SetBool("Give", true);
            MovementActions.StartTurn(otherAgent, agent.transform.position);
        }

        void finishHandoff()
        {
            if (agent != null && agent.Anim != null)
                agent.Anim.SetBool("Give", false);
            interchanged.value = true;
        }

        IEnumerator onRaise()
        {
            yield return null;

            if (otherAgent == null)
            {
                context.ReportActionError($"Cannot give item to '{recipientName}': recipient agent no longer exists.", agent, action);
                yield break;
            }

            GrabbableItem item = agent.Hand.Held;
            if (item == null)
            {
                context.ReportActionError($"Cannot give item to '{recipientName}': '{agent.name}' is no longer holding an item.", agent, action);
                yield break;
            }

            IEnumerator grabRoutine = otherAgent.Hand.Grab(item, finishHandoff);
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = grabRoutine.MoveNext();
                }
                catch (Exception ex)
                {
                    context.ReportActionError(ex.Message, agent, action);
                    yield break;
                }

                if (!hasNext) break;
                yield return grabRoutine.Current;
            }

            finishHandoff();
        }

        IEnumerator animationChain =
            Utils.MonitorAnimatorState(agent.Anim, "Give_Raise", 1)
            .OnCrEnd(onRaise())
            .OnCrEnd(Utils.MonitorFlag(interchanged))
            .OnCrEnd(Utils.MonitorAnimatorState(agent.Anim, "Give_lower", 1));

        IEnumerator behavior()
        {
            yield return null; // ensures anim.coroutine is assigned before CancelPlan can run
            if (error != null)
            {
                context.ReportActionError(error, agent, action);
                yield break;
            }

            yield return animationChain;
        }

        void end()
        {
            if (agent != null && agent.Anim != null)
                agent.Anim.SetBool("Give", false);

            if (otherAgent != null && otherAgent.Hand != null && otherAgent.Hand.State == HandState.Grabbing)
                otherAgent.Hand.ForceRelease();
        }

        result = new AnimAction(action, start, behavior(), end);
        return null;
    }
}