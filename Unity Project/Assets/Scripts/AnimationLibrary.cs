using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AnimationLibrary : MonoBehaviour
{
    public List<Animation> animations = new();
    public ulong id;

    private void Start()
    {
        StartCoroutine(AnimationManagerCoroutine());
    }

    public string PlayAnimation(AgentSystem context, ActionData action)
    {
        var param = action.parameters;

        switch (action.name)
        {
            case "moveToSpot":
                ContextItem obj = context.contextLibrary.spots.Find(x => x.GetName() == param[0]);
                if (obj == null) return $"Couldn't find spot with name {param[0]}";

                Move(context.contextLibrary.agent.Nav, obj.transform.position, action);
                break;
            case "talk":
                Talk(context.chatManager, param[0], action);
                break;
            case "moveToPoint":
                if (param.Length < 3)
                {
                    return "moveToPoint requires 3 parameters: x, y, z";
                }
                Move(context.contextLibrary.agent.Nav, ToVec3(param[0], param[1], param[2]), action);
                break;
            case "count": //for testing purposes
                Count(context.chatManager, int.Parse(param[0]), action);
                break;
            case "grab":
                obj = context.contextLibrary.environment.Find(x => x.GetName() == param[0]);
                if (obj == null) return $"Couldn't find spot with name {param[0]}";
                
                Grab(context.contextLibrary.agent, obj, action);
                break;
            case "place":
                Place(context.contextLibrary.agent, action);
                break;
            default:
                return $"Unknown action: {action.name}";
        }

        return null;
    }

    private IEnumerator AnimationManagerCoroutine()
    {
        while (true)
        {
            for (int i = animations.Count - 1; i >= 0; i--)
            {
                var anim = animations[i];

                if (anim.isFinished)
                {
                    animations.RemoveAt(i);
                    continue;
                }

                if (anim.isPlaying) continue;

                if (anim.data.runAfter != null && anim.data.runAfter.Length > 0)
                {
                    bool canRun = true;
                    foreach (var id in anim.data.runAfter)
                    {
                        if (animations.Exists(a => a.data.id == id))
                        {
                            canRun = false;
                            break;
                        }
                    }
                    if (!canRun) continue;
                }

                if (anim.data.delayBefore > 0)
                {
                    anim.data.delayBefore -= Time.deltaTime; // Changed to deltaTime for standard coroutine
                    continue;
                }

                anim.isPlaying = true;
                StartCoroutine(ExecuteAnimation(anim));
            }

            yield return null;
        }
    }

    private IEnumerator ExecuteAnimation(Animation anim)
    {
        if (anim.start != null)
        {
            anim.start.Invoke();
            anim.start = null;
        }

        if (anim.behavior != null)
        {
            yield return anim.behavior;
        }

        anim.end?.Invoke();
        anim.isFinished = true;
    }

    public Animation PushAnimation(ActionData data, IEnumerator behavior, Action start = null, Action end = null)
    {
        var anim = new Animation(data, behavior, start, end);
        if (animations.Exists(a => a.data.id == data.id))
        {
            Debug.LogWarning($"Animation with id {data.id} already exists. LLM might have hallucinated.");
            return null;
        }
        animations.Add(anim);
        return anim;
    }

    Vector3 ToVec3(string px, string py, string pz)
    {
        float.TryParse(px, out float x);
        float.TryParse(py, out float y);
        float.TryParse(pz, out float z);
        return new Vector3(x, y, z);
    }

    void Count(ChatManager chat, int total, ActionData action)
    {
        IEnumerator behavior()
        {
            int count = 0;

            while (count <= total)
            {
                chat.AddChat($"Count: {count++}");

                yield return new WaitForSeconds(1f);
            }
        }

        PushAnimation(action, behavior());
    }

    void Talk(ChatManager chat, string msg, ActionData action)
    {
        void start()
        {
            chat.AddChat(msg);
        }
        PushAnimation(action, null, start);
    }

    void Move(NavMeshAgent agent, Vector3 pos, ActionData action)
    {
        var animator = agent.GetComponentInChildren<Animator>();
        void start()
        {
            animator.SetBool("Walking", true);
            agent.SetDestination(pos);
        }

        void end()
        {
            animator.SetBool("Walking", false);
        }

        IEnumerator behavior = Utils.MonitorMovement(agent);
        PushAnimation(action, behavior, start, end);
    }

    // now this is primitive, but it will do for now. We can improve this later with IK and other techniques.
    void Grab(NPC agent, ContextItem item, ActionData action)
    {
        Flag grabbed = new();
        void start()
        {
            agent.Anim.SetTrigger("Grab");
            agent.GrabReceiver.OnGrabPoint += snapObjectToHand;
        }

        void snapObjectToHand()
        {
            agent.GrabItem(item.transform, true);
            grabbed.value = true;
        }

        void end()
        {
            agent.GrabReceiver.OnGrabPoint -= snapObjectToHand;
        }

        PushAnimation(action, Utils.MonitorFlag(grabbed), start, end);
    }

    void Place(NPC agent, ActionData action)
    {
        Flag hasReleased = new();

        void start()
        {
            agent.Anim.SetTrigger("Grab");
            agent.GrabReceiver.OnGrabPoint += releaseItem;
        }

        void releaseItem()
        {
            Transform item = agent.ReleaseItem(right: true);

            if (item != null)
            {
                Vector3 placePos = ToVec3(action.parameters[0], action.parameters[1], action.parameters[2]);
                item.position = placePos;
            }
            else
            {
                Debug.LogWarning("Agent tried to place an item but wasn't holding anything!");
            }

            hasReleased.value = true;
        }

        void end()
        {
            agent.GrabReceiver.OnGrabPoint -= releaseItem;
        }

        

        PushAnimation(action, Utils.MonitorFlag(hasReleased), start, end);
    }
}