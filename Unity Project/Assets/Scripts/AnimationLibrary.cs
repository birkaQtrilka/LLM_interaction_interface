using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AnimationLibrary : MonoBehaviour
{

    public string PlayAnimation(ContextLibrary context, string animationName, string[] param)
    {
        switch(animationName)
        {
            case "moveToSpot":
                ContextItem obj = context.spots.Find(x => x.name == param[0]);
                if (obj == null) {
                    return $"Couldn't find spot with name {param[0]}";
                }
                Move(context.agent, obj.transform.position);
            break;
            case "talk":
                
                break;
        }

        return null;
    }

    void Move(NavMeshAgent agent, Vector3 pos)
    {
        agent.SetDestination(pos);
    }
}
