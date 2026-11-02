using System;
using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(RaycastHitEvents))]
public class AgentDestinationSetter : MonoBehaviour
{
    [SerializeField] NPC agent;
    [SerializeField] AgentSystem system;

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
        // add if it doesn't exist in the context library
        if (system.GetObject(tr.name) == null) system.contextLibrary.environment.Add(new ContextItem { transform = tr});
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

    private void Update()
    {
        if(Keyboard.current.fKey.wasPressedThisFrame)
        {
            if(agent.Hand.State == HandState.Holding)
            {
                StartCoroutine(agent.Hand.ReleaseAt(null, tr => Debug.Log($"Released {tr.name}")));
            }
        }
    }

    private void OnDisable()
    {
        raycast.onSpacePressed.RemoveListener(SetDestination);
    }

}
