using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationLibrary : MonoBehaviour
{
    public List<AnimationInstance> animations = new();
    public ulong id;

    private void Start()
    {
        StartCoroutine(AnimationManagerCoroutine());
    }

    public string PlayAnimation(AgentSystem context, ActionData action)
    {
        var param = action.parameters;
        NPC agent = context.GetAgent(action.agent);
        if (agent == null) return $"Couldn't find agent with name {action.agent}";

        switch (action.name)
        {
            case "moveTo":
                string name = param[0];
                Transform obj = context.GetSpot(name)?.transform;
                obj ??= context.GetObject(name)?.transform;
                obj ??= context.GetAgent(name)?.transform;
                if (obj == null) return $"Couldn't find spot with name {param[0]}";

                return ExecuteAction(Actions.Move(agent, obj.position, action));
            case "talk":
                return ExecuteAction(Actions.Talk(context.chatManager, param[0], action));
            case "moveToPoint":
                if (param.Length < 3) return "moveToPoint requires 3 parameters: x, y, z"; 

                return ExecuteAction(Actions.Move(agent, ToVec3(param[0], param[1], param[2]), action));
            case "count": //for testing purposes

                return ExecuteAction(Actions.Count(context.chatManager, int.Parse(param[0]), action));
            case "grab":
                var objToGrab = context.GetObject(param[0]);
                if (objToGrab == null) return $"Couldn't find object with name {param[0]}";
                
                return ExecuteAction(Actions.Grab(agent, objToGrab, action));
            case "place":
                if (param.Length != 1) return "place requires 1 string parameter";

                return ExecuteAction(Actions.Place(agent, context.contextLibrary.environment, action));
            case "give":

                return ExecuteAction(Actions.Give(agent, context, action));
            default:
                return $"Unknown action: {action.name}";
        }

    }

    public string ExecuteAction(AnimAction exe)
    {
        return PushAnimation(exe.data, exe.behavior, exe.start, exe.end) != null ? null : $"Action with id {exe.data.id} already exists"; 
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

    private IEnumerator ExecuteAnimation(AnimationInstance anim)
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

    public AnimationInstance PushAnimation(ActionData data, IEnumerator behavior, Action start = null, Action end = null)
    {
        var anim = new AnimationInstance(data, behavior, start, end);
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

}