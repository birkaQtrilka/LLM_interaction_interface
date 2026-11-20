using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimationLibrary : MonoBehaviour
{
    [field: SerializeField] public DialoguePlayer DialoguePlayer {get; private set; }

    //public List<AnimationClip> clips = new();
    public List<AnimationInstance> animations = new();
    public ulong id;

    readonly Dictionary<string, IAgentAction> registry = new();
    public event Action<AnimationInstance> AnimationStarted;
    public event Action<AnimationInstance> AnimationEnded;
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

    public string PlayAnimation(AgentSystem context, ActionData action, int planId = 0)
    {
        if (!registry.TryGetValue(action.name, out var handler))
            return $"Unknown action: {action.name}";

        NPC agent = context.GetAgent(action.agent);
        if (agent == null) return $"Couldn't find agent with name {action.agent}";

        string error = handler.TryBuild(action, context, agent, out AnimAction result);
        if (error != null)
        {
            Debug.LogError(error);
            return error;
        }

        return ExecuteAction(result, planId);
    }

    public string ExecuteAction(AnimAction exe, int planId = 0)
    {
        return PushAnimation(exe.data, exe.behavior, exe.start, exe.end, planId) != null
            ? null
            : $"Action with id {exe.data.id} already exists";
    }

    public AnimationInstance PushAnimation(ActionData data, IEnumerator behavior, Action start = null, Action end = null, int planId = 0)
    {
        if (animations.Exists(a => a.data.id == data.id))
        {
            Debug.LogWarning($"Animation with id {data.id} already exists. LLM might have hallucinated.");
            return null;
        }
        var anim = new AnimationInstance(data, behavior, start, end) { planId = planId };
        animations.Add(anim);
        return anim;
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
                anim.coroutine = StartCoroutine(ExecuteAnimation(anim));
            }

            yield return null;
        }
    }

    private IEnumerator ExecuteAnimation(AnimationInstance anim)
    {
        if (anim.start != null)
        {
            Debug.Log($"Starting animation {anim.data.name} (id: {anim.data.id})");
            anim.start?.Invoke();
            anim.start = null;
            AnimationStarted?.Invoke(anim);
        }

        if (anim.behavior != null)
        {
            yield return anim.behavior;
        }
        AnimationEnded?.Invoke(anim);

        anim.end?.Invoke();
        anim.isFinished = true;
    }

    public string ShowAnimationActions()
    {
        var jsonObjects = animations
            .Where(animation => animation != null && animation.data != null)
            .Select(animation => JsonUtility.ToJson(animation.data, true));

        return "[\n" + string.Join(",\n", jsonObjects) + "\n]";
    }
    
    public AnimationInstance GetAnimation(Predicate<AnimationInstance> predicate)
    {
        return animations.Find(predicate);
    }

    void Cancel(AnimationInstance a)
    {
        if (a.isFinished) return;

        bool wasPlaying = a.isPlaying;
        if (a.coroutine != null) StopCoroutine(a.coroutine); // also stops the nested behavior
        a.isFinished = true; // the manager removes it on its next tick

        if (wasPlaying) // end() only makes sense if start() ran
        {
            AnimationEnded?.Invoke(a);
            a.end?.Invoke();
        }
    }

    /// <summary>
    /// Stops every unfinished animation that belongs to the given plan and reports what was stopped.
    /// wasRunning = false means it was still queued (its start() never ran).
    /// Don't call this from inside an action's start(): that runs inside the manager's loop.
    /// </summary>
    public List<(AnimationInstance anim, bool wasRunning)> CancelPlan(int planId)
    {
        var cancelled = new List<(AnimationInstance, bool)>();
        if (planId <= 0) return cancelled;

        foreach (var a in animations.ToArray()) // copy: Cancel fires events that could touch the list
        {
            if (a.planId != planId || a.isFinished) continue;
            bool wasRunning = a.isPlaying; // read before Cancel, which doesn't reset it
            Cancel(a);
            cancelled.Add((a, wasRunning));
        }
        return cancelled;
    }
}