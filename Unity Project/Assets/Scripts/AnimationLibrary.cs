using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
[System.Serializable]
public class Animation
{
    public readonly ulong id;
    public readonly string name;
    [NonSerialized] public Coroutine coroutine;

    public Animation(ulong id, string name, Coroutine coroutine)
    {
        this.id = id;
        this.name = name;
        this.coroutine = coroutine;
    }
}

public class AnimationLibrary : MonoBehaviour
{
    public List<Animation> animations = new();
    public ulong id;

    public string PlayAnimation(AgentSystem context, string animationName, string[] param)
    {
        switch(animationName)
        {
            case "moveToSpot":
                ContextItem obj = context.contextLibrary.spots.Find(x => x.name == param[0]);
                if (obj == null) {
                    return $"Couldn't find spot with name {param[0]}";
                }
                Move(context.contextLibrary.agent, obj.transform.position);
            break;
            case "talk":
                context.chatManager.AddChat(param[0]);
                break;
            case "moveToPoint":
                if (param.Length < 3) {
                    return "moveToPoint requires 3 parameters: x, y, z";
                }
                Move(context.contextLibrary.agent, ToVec3(param[0], param[1], param[2]));
                break;
        }

        return null;
    }

    void Move(NavMeshAgent agent, Vector3 pos)
    {
        agent.SetDestination(pos);
        Animation anim = null;
        Coroutine cr = StartCoroutine(Utils.MonitorMovement(agent, () => {
            PopAnimation(anim);
        }));
        anim = PushAnimation("move", cr);
    }

    public Animation PushAnimation(string name, Coroutine cr)
    {
        var anim = new Animation(id++, name, cr);
        animations.Add(anim);
        return anim;
    }

    public void PopAnimation(Animation anim)
    {
        int index = animations.FindIndex(a => a.id == anim.id);
        if (index != -1)
        {
            animations.RemoveAt(index);
        }
    }

    Vector3 ToVec3(string px, string py, string pz)
    {
        float.TryParse(px, out float x);
        float.TryParse(py, out float y);
        float.TryParse(pz, out float z);
        return new Vector3(x, y, z);
    }
}
