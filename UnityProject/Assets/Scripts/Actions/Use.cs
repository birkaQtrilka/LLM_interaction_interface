using System.Collections;
using UnityEngine;

public class Use : IAgentAction
{
    public string Name => "use";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        var param = action.parameters;
        if (param.Length != 1) return "requires 1 parameter: useOn (item the object in hand should be used on)";

        string error = null;
        UseSession session = null;
        NPCUseController controller = null;
        ResolvedUseInteraction resolved = default;

        void start()
        {
            // Resolved at actual start: the held item may have changed since queuing.
            Transform target = context.GetObject(param[0])?.transform ?? context.GetAgent(param[0])?.transform;
            if (target == null) { error = $"Invalid use target: could not find object or agent '{param[0]}'."; return; }

            Transform held = agent.GetItem(); // real held object, not a name lookup
            ContextItem legacy = held != null ? context.GetObject(held.name) : null;

            error = UseInteractionResolver.Resolve(target, held, legacy, out resolved);
            if (error != null) return;

            controller = agent.GetComponent<NPCUseController>();
            if (controller == null) { error = $"Misconfigured: '{agent.name}' has no NPCUseController."; return; }

            session = controller.Begin(resolved, out error);
        }

        IEnumerator behavior()
        {
            yield return new WaitUntil(() => error != null || session != null);
            if (error != null) { context.ReportActionError(error, agent, action); yield break; }
            while (!session.Done) yield return null; // bounded by the controller's own timeouts
            if (session.Error != null) { context.ReportActionError(session.Error, agent, action); yield break; }

            // A used-up item stays on the target: free the hand and hide it, so it also drops out of the context
            if (resolved.UsesHeldItem && resolved.Profile.ConsumesHeldItem && resolved.Target != resolved.HeldObject)
            {
                agent.ReleaseItem();
                resolved.HeldObject.gameObject.SetActive(false);
            }
        }

        void end() { if (controller != null) controller.Cancel(session); }

        result = new AnimAction(action, start, behavior(), end);
        return null;
    }
}