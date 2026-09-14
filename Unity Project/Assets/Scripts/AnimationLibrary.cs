using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class AnimationLibrary : MonoBehaviour
{
    const float GrabRange = 2f;

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
            case "lookAt":
                if (param.Length < 1) {
                    return "lookAt requires a target name";
                }
                ContextItem lookTarget = context.contextLibrary.spots.Find(x => x.name == param[0]);
                if (lookTarget == null) {
                    lookTarget = context.contextLibrary.environment.Find(x => x.name == param[0]);
                }
                if (lookTarget == null) {
                    return $"Couldn't find look target {param[0]}";
                }
                context.contextLibrary.agent.transform.LookAt(lookTarget.transform);
                break;
            case "grab":
                if (param.Length < 1) return "grab requires an object name";
            
                ContextItem grabItem = context.contextLibrary.environment.Find(x => x.name == param[0]);
                if (grabItem == null) return $"Couldn't find object {param[0]}";
                NavMeshAgent agent = context.contextLibrary.agent;
                float dist = Vector3.Distance(agent.transform.position, grabItem.transform.position);
                if (dist > GrabRange)
                {
                    StartCoroutine(WalkThenGrab(agent, grabItem));
                    break;
                }
                Hold(agent, grabItem);
                break;
            default:
                return $"Unknown action: {animationName}";
        }

        return null;
    }

    IEnumerator WalkThenGrab(NavMeshAgent agent, ContextItem grabItem)
    {
        Move(agent, grabItem.transform.position);
        // SetDestination is async; wait until NavMesh says we are close enough.
        while (agent.pathPending || agent.remainingDistance > GrabRange)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
            {
                break;
            }
            yield return null;
        }
        Hold(agent, grabItem);
    }

    void Hold(NavMeshAgent agent, ContextItem grabItem)
    {
        grabItem.transform.SetParent(agent.transform);
        // Hold beside the capsule until there is a hand bone.
        grabItem.transform.localPosition = new Vector3(0.4f, 1f, 0.4f);
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
