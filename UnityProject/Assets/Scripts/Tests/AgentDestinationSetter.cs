using System;
using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(RaycastHitEvents))]
public class AgentDestinationSetter : MonoBehaviour
{
    enum HitAction
    {
        GoAndUse,
        GoAndGrab
    }

    [SerializeField] NPC agent;
    [SerializeField] AgentSystem system;
    [SerializeField] HitAction hitAction;
    RaycastHitEvents raycast;
    private void Awake()
    {
        raycast = GetComponent<RaycastHitEvents>();
    }
    private void OnEnable()
    {
        raycast.onSpacePressed.AddListener(SetDestination);

    }

    private void SetDestination(Transform tr, Transform obj)
    {
        switch (hitAction)
        {
            case HitAction.GoAndUse:
                GoAndUse(tr, obj);
                break;
            case HitAction.GoAndGrab:
                GoAndGrab(tr, obj);
                break;
            default:
                break;
        }
    }

    public void GoAndGrab(Transform tr, Transform obj)
    {
        if (system.GetObject(tr.name) == null) system.contextLibrary.environment.Add(new ContextItem { transform = tr });
        system.AnimationLibrary.PlayAnimation(system,
           new ActionData
           {
               id = 0,
               name = "moveTo",
               agent = agent.name,
               parameters = new string[] { tr.name }
           }
       );
        var grab = obj.GetComponentInParent<GrabbableItem>();
        if (grab == null) return;
        system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = 1,
                name = "grab",
                agent = agent.name,
                runAfter = new int[] { 0 },
                parameters = new string[] { grab.name }
            }

        );
    }

    public void GoAndUse(Transform tr, Transform obj)
    {
        if (system.GetObject(tr.name) == null) system.contextLibrary.environment.Add(new ContextItem { transform = tr });
        EnvironmentItemTag itemToUseOn = GetComponentInParents<EnvironmentItemTag>(obj);
        if(itemToUseOn == null)
        {
            Debug.LogWarning($"Object {obj.name} does not have an EnvironmentItemTag component in its parents. Cannot use.");
            return;
        }
        int id = 0;
        system.AnimationLibrary.PlayAnimation(system,
           new ActionData
           {
               id = id++,
               name = "moveTo",
               agent = agent.name,
               parameters = new string[] { tr.name }
           }
        );

        if(itemToUseOn.TryGetComponent<GrabbableItem>(out var item))
        {
            system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = id++,
                name = "grab",
                agent = agent.name,
                runAfter = new int[] { id - 2 },
                parameters = new string[] { itemToUseOn.name }
            }

        );
        }
        
        system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = id++,
                name = "use",
                agent = agent.name,
                runAfter = new int[] { id - 2 },
                parameters = new string[] { itemToUseOn.name }
            }
        
        );
    }

    T GetComponentInParents<T>(Transform tr) where T : Component
    {
        if (tr == null) return null;
        if (tr.TryGetComponent<T>(out var comp)) return comp;
        return GetComponentInParents<T>(tr.parent);
    }

    private void Update()
    {
        if(Keyboard.current.fKey.wasPressedThisFrame)
        {
            if(agent.Hand.State == HandState.Holding)
            {
                StartCoroutine(agent.Hand.ReleaseAt(null, tr => Debug.Log($"Released {tr.name}")));
            }
        }
        if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            hitAction = HitAction.GoAndUse;
        }
        if (Keyboard.current.zKey.wasPressedThisFrame)
        {
            hitAction = HitAction.GoAndGrab;
        }

    }



    private void OnDisable()
    {
        raycast.onSpacePressed.RemoveListener(SetDestination);
    }

}
