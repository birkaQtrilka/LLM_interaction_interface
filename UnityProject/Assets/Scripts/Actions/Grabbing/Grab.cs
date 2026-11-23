using System;
using System.Collections;
using UnityEngine;

public class Grab : IAgentAction
{
    public string Name => "grab";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters == null || action.parameters.Length != 1)
            return "grab requires 1 parameter: object name";

        string objectName = action.parameters[0];
        if (string.IsNullOrEmpty(objectName))
            return "grab requires a non-empty object name";

        var obj = context.GetObject(objectName);
        if (obj == null) return $"Couldn't find object with name '{objectName}'";

        var item = obj.transform.GetComponent<GrabbableItem>();
        if (item == null || item.profile == null) return $"'{objectName}' is not grabbable";

        if (agent.Hand == null) return $"Misconfigured: '{agent.name}' has no HandController.";
        LookHandle lookHandle = null;
        // C# does not allow yield return inside a try block with a catch clause,
        // behavior() manually steps grabRoutine.MoveNext() inside try/catch and yields grabRoutine.Current outside it. 
        IEnumerator behavior()
        {
            yield return null; // ensures anim.coroutine is assigned before CancelPlan can run

            if (item == null)
            {
                context.ReportActionError($"Cannot grab '{objectName}': object no longer exists.", agent, action);
                yield break;
            }
            lookHandle = agent.Look.Request(() => item.GripTarget != null ? item.GripTarget.position : item.transform.position);

            IEnumerator grabRoutine = agent.Hand.Grab(item);
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
        }

        void end()
        {
            agent.Look.Release(lookHandle, agent.GrabLinger);
            lookHandle = null;
            if (agent.Hand != null && agent.Hand.State == HandState.Grabbing)
                agent.Hand.ForceRelease();
        }

        result = new AnimAction(action, null, behavior(), end);
        return null;
    }
}