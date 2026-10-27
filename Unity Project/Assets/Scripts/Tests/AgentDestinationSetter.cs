using System;
using UnityEngine;
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

    private void SetDestination(Transform tr)
    {
        if(system.GetObject(tr.name) == null) system.contextLibrary.environment.Add(new ContextItem { transform = tr});
        system.AnimationLibrary.PlayAnimation(system,
           new ActionData
           {
               id = 0,
               name = "moveTo",
               agent = agent.name,
               parameters = new string[] { tr.name }
           }
       );
    }

    private void OnDisable()
    {
        raycast.onSpacePressed.RemoveListener(SetDestination);
    }

}
