using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
                obj ??= context.contextLibrary.environment.Find(x => x.GetName() == param[0]);
                if (obj == null) return $"Couldn't find spot with name {param[0]}";

                ExecuteAction(Actions.Move(context.contextLibrary.agent, obj.transform.position, action));
                break;
            case "talk":
                ExecuteAction(Actions.Talk(context.chatManager, param[0], action));
                break;
            case "moveToPoint":
                if (param.Length < 3)
                {
                    return "moveToPoint requires 3 parameters: x, y, z";
                }
                ExecuteAction(Actions.Move(context.contextLibrary.agent, ToVec3(param[0], param[1], param[2]), action));
                break;
            case "count": //for testing purposes
                ExecuteAction(Actions.Count(context.chatManager, int.Parse(param[0]), action));
                break;
            case "grab":
                obj = context.contextLibrary.environment.Find(x => x.GetName() == param[0]);
                if (obj == null) return $"Couldn't find object with name {param[0]}";
                
                ExecuteAction(Actions.Grab(context.contextLibrary.agent, obj, action));
                break;
            case "place":
                if (param.Length != 1) return "moveToPoint requires 1 string parameter";
                ExecuteAction(Actions.Place(context.contextLibrary.agent, action, context.contextLibrary.environment));
                break;
            default:
                return $"Unknown action: {action.name}";
        }

        return null;
    }

    public void ExecuteAction(AnimAction exe)
    {
        PushAnimation(exe.data, exe.behavior, exe.start, exe.end);
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

}