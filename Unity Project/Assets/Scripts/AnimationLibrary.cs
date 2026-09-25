using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimationLibrary : MonoBehaviour
{
    public List<AnimationInstance> animations = new();
    public ulong id;

    readonly Dictionary<string, IAgentAction> registry = new();

    private void Awake()
    {
        BuildRegistry();
    }

    void BuildRegistry()
    {
        var actionTypes = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().SelectMany(SafeGetTypes)
            .Where(t => typeof(IAgentAction).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var type in actionTypes)
        {
            IAgentAction instance;
            try
            {
                instance = (IAgentAction)Activator.CreateInstance(type);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create IAgentAction instance for {type.Name}: {e.Message}");
                continue;
            }

            if (registry.TryGetValue(instance.Name, out var existing))
            {
                Debug.LogError($"Duplicate action name \"{instance.Name}\": {existing.GetType().Name} and {type.Name} both claim it. Keeping {existing.GetType().Name}.");
                continue;
            }

            registry[instance.Name] = instance;
        }

        Debug.Log($"AnimationLibrary registered {registry.Count} actions: {string.Join(", ", registry.Keys)}");
    }

    static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException e)
        {
            // Some assemblies (editor-only, third-party with missing refs) can fail to fully load types.
            // Fall back to whatever did load successfully instead of losing the whole assembly's actions.
            return e.Types.Where(t => t != null);
        }
    }

    private void Start()
    {
        StartCoroutine(AnimationManagerCoroutine());
    }

    public string PlayAnimation(AgentSystem context, ActionData action)
    {
        if (!registry.TryGetValue(action.name, out var handler))
            return $"Unknown action: {action.name}";

        NPC agent = context.GetAgent(action.agent);
        if (agent == null) return $"Couldn't find agent with name {action.agent}";

        string error = handler.TryBuild(action, context, agent, out AnimAction result);
        if (error != null) return error;

        return ExecuteAction(result);
    }

    public string ExecuteAction(AnimAction exe)
    {
        return PushAnimation(exe.data, exe.behavior, exe.start, exe.end) != null
            ? null
            : $"Action with id {exe.data.id} already exists";
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