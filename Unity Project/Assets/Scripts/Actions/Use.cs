using System.Collections;
using System.Linq;
using UnityEngine;

public enum NPC_AnimLayer
{
    Locomotion = 0,
    Arms = 1,
    Gestures = 2,
    ItemUse = 3,
}

public class Use : IAgentAction
{
    public string Name => "use";
    // assuming item can be used only one way, and npc has one animation for using it, and the item has one animation for being used
    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        var param = action.parameters;
        if (param.Length != 1) return "requires 1 parameter: useOn (item the object in hand should be used on)";

        ContextItem itemInHand = null;
        int nameHash = -1;

        void start()
        {
            (string err, Transform useOn, ContextItem it) = Validate(param[0], context, agent);
            if (err != null) throw new System.Exception(err);
            itemInHand = it;
            nameHash = Animator.StringToHash(itemInHand.GetName());
            agent.Anim.Play(nameHash, (int)NPC_AnimLayer.ItemUse);
            agent.GrabReceiver.EventRelay += OnEvent;

        }

        void OnEvent(string name)
        {
            agent.GrabReceiver.EventRelay -= OnEvent;
            var anim = itemInHand.transform.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetTrigger("Use");// can be Play(name) later
            }
            else 
                Debug.LogWarning($"Item: '{itemInHand.GetName()}' does not have an Animator component to play the 'Use' animation");
        }
        IEnumerator behavior()
        {
            yield return new WaitUntil(() => nameHash != -1);
            yield return Utils.MonitorAnimatorState(agent.Anim, nameHash, (int)NPC_AnimLayer.ItemUse);
        }
        void end()
        {
            agent.GrabReceiver.EventRelay -= OnEvent;
        }
        // hold starts from down, it needs to be like idle where it's just in one spot 
        result = new AnimAction(
            action, 
            start, 
            behavior(), 
            end
        );

        // play animation
        return null;
    }

    (string err, Transform useOn, ContextItem itemInHand) Validate(string param, AgentSystem context, NPC agent)
    {
        Transform useOn = context.GetObject(param)?.transform ?? context.GetAgent(param)?.transform;
        if (useOn == null) return ($"could not find object or agent with name '{param}'", null, null);
        var item = agent.GetItem();
        if (item == null) return ($"agent '{agent.name}' is not holding an item in their right hand", null, null);
        ContextItem itemInHand = context.GetObject(item.name);
        if (itemInHand == null) return ($"Item: '{item}' is not saved in environment objects", null, null);
        if (!itemInHand.usableOn.Contains(useOn)) return ($"Item: '{item}' cannot be used on '{useOn.name}'", null, null);
        return (null, useOn, itemInHand);
    }
}
