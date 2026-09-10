using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AnimationLibrary : MonoBehaviour
{

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
    }

    Vector3 ToVec3(string px, string py, string pz)
    {
        float.TryParse(px, out float x);
        float.TryParse(py, out float y);
        float.TryParse(pz, out float z);
        return new Vector3(x, y, z);
    }
}
