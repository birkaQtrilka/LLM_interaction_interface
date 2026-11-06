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

        int nameHash = -1;
        string error = null;

        void start()
        {
            (string err, Transform useOn, ContextItem it) = Validate(param[0], context, agent);
            error = err;
            if (error != null) return;

            if (it == null) // nothing in hands
            {
                var itemAnim = useOn.GetComponentInChildren<Animator>();
                if (itemAnim != null)
                {
                    itemAnim.SetTrigger("Use");
                    nameHash = Animator.StringToHash(useOn.name);
                }
                else error = $"Item: '{useOn.name}' does not have an Animator component to play the 'Use' animation";
            }
            else // with item in hands
            {
                var itemAnim = it.transform.GetComponentInChildren<Animator>();
                if (itemAnim != null)
                {
                    itemAnim.SetTrigger("Use");
                    nameHash = Animator.StringToHash(it.GetName());
                }
                else error = $"Item: '{it.GetName()}' does not have an Animator component to play the 'Use' animation";
            }

            if (nameHash != -1) agent.Anim.Play(nameHash, (int)NPC_AnimLayer.ItemUse);
        }

        IEnumerator behavior()
        {
            if (error != null)
            {
                Debug.LogError(error);
                yield break;
            }

            yield return new WaitUntil(() => error != null || nameHash != -1);
            if (error != null)
            {
                Debug.LogError(error);
                yield break;
            }
            yield return Utils.MonitorAnimatorState(agent.Anim, nameHash, (int)NPC_AnimLayer.ItemUse);
        }

        result = new AnimAction(action, start, behavior(), null);

        return null;
    }

    (string err, Transform useOn, ContextItem itemInHand) Validate(string param, AgentSystem context, NPC agent)
    {
        Transform useOn = context.GetObject(param)?.transform ?? context.GetAgent(param)?.transform;
        if (useOn == null) return ($"could not find object or agent with name '{param}'", null, null);
        var item = agent.GetItem();
        ContextItem itemInHand = item == null ? null : context.GetObject(item.name);
        return (null, useOn, itemInHand);
    }
}