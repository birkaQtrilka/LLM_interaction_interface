using System.Collections;
using UnityEngine;

public class Use : IAgentAction
{
    public string Name => "use";
    // assuming item can be used only one way, and npc has one animation for using it, and the item has one animation for being used
    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        var param = action.parameters;
        if (param.Length != 1) return "requires 1 parameter: useOn (item the object in hand should be used on)";

        int nameHash = -1;

        void start()
        {
            (string err, Transform useOn, ContextItem it) = Validate(param[0], context, agent);
            if (err != null) throw new System.Exception(err);
            
            if(it == null) // nothing in hands
            {
                var itemAnim = useOn.GetComponentInChildren<Animator>();
                if (itemAnim != null)
                {
                    itemAnim.SetTrigger("Use");// can be Play(name) later
                } else Debug.LogWarning($"Item: '{useOn.name}' does not have an Animator component to play the 'Use' animation");

                nameHash = Animator.StringToHash(useOn.name);
            }
            else // with item in hands
            {
                var itemAnim = it.transform.GetComponentInChildren<Animator>();
                if (itemAnim != null)
                {
                    itemAnim.SetTrigger("Use");
                    // The "Use" trigger should be set up in the Animator for the item to play the appropriate animation when used only if Immediate
                    // If you want to play the animation later, in a timed Matter, you should use "UseLater" together with an animation behavior/event
                } else Debug.LogWarning($"Item: '{it.GetName()}' does not have an Animator component to play the 'Use' animation");
                nameHash = Animator.StringToHash(it.GetName());
            }

            agent.Anim.Play(nameHash, (int)NPC_AnimLayer.ItemUse);
        }

        IEnumerator behavior()
        {
            yield return new WaitUntil(() => nameHash != -1);
            yield return Utils.MonitorAnimatorState(agent.Anim, nameHash, (int)NPC_AnimLayer.ItemUse);
        }

        result = new AnimAction(
            action, 
            start, 
            behavior(), 
            null
        );

        // play animation
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
