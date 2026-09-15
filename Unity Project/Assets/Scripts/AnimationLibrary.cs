using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class Animation
{
    public ActionData data;
    public IEnumerator behavior;
    public Action start;
    public Action end;

    public bool isPlaying;
    public bool isFinished;

    public Animation(ActionData data, IEnumerator behavior, Action start, Action end)
    {
        this.data = data;
        this.behavior = behavior;
        this.start = start;
        this.end = end;
        this.isPlaying = false;
        this.isFinished = false;
    }

    private Animation() { }

    public override string ToString()
    {
        string dependencies = data.runAfter != null ? string.Join(", ", data.runAfter) : "";

        return $"Animation: {data?.name}, ID: {data?.id}, isPlaying: {isPlaying}, dependentOn: [{dependencies}], delayBefore: {data?.delayBefore}";
    }

}

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
                ContextItem obj = context.contextLibrary.spots.Find(x => x.name == param[0]);
                if (obj == null) return $"Couldn't find spot with name {param[0]}";

                Move(context.contextLibrary.agent, obj.transform.position, action);
                break;
            case "talk":
                Talk(context.chatManager, param[0], action);
                break;
            case "moveToPoint":
                if (param.Length < 3)
                {
                    return "moveToPoint requires 3 parameters: x, y, z";
                }
                Move(context.contextLibrary.agent, ToVec3(param[0], param[1], param[2]), action);
                break;
            case "count": //for testing purposes
                Count(context.chatManager, int.Parse(param[0]), action);
                break;
            case "grab":
                obj = context.contextLibrary.environment.Find(x => x.name == param[0]);
                if (obj == null) return $"Couldn't find spot with name {param[0]}";
                
                Grab(context.contextLibrary.agent, obj, action);
                break;
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

    void Grab(NavMeshAgent agent, ContextItem item, ActionData action)
    {
        var animator = agent.GetComponentInChildren<Animator>();
        void start()
        {
            animator.SetTrigger("Grab");
            // Implement logic to move the item to the agent's hand or inventory
        }
        IEnumerator behavior = Utils.MonitorMovement(agent);
        PushAnimation(action, behavior, start);
    }
}