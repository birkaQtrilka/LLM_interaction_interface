using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public struct LookSettings
{
    public float MaxSightDistance;
    public float CheckInterval;
    public float ItemTolerance;

    public LookSettings(float maxSightDistance, float checkInterval, float itemTolerance)
    {
        MaxSightDistance = maxSightDistance;
        CheckInterval = checkInterval;
        ItemTolerance = itemTolerance;
    }
}

public class LookAtHandler
{
    readonly NPC agent;
    readonly Func<Vector3> getTargetPosition;
    readonly LayerMask sightMask;

    Coroutine lookRoutine;
    Coroutine watchRoutine;
    LookSettings lookSettings;

    public LookAtHandler(NPC agent, Func<Vector3> getTargetPosition, LayerMask sightMask = default)
    {
        this.agent = agent;
        this.getTargetPosition = getTargetPosition;
        this.sightMask = sightMask == default ? Physics.DefaultRaycastLayers : sightMask;
    }

    public LookAtHandler(NPC agent, TransOrPos target, LayerMask sightMask = default)
        : this(agent, () => target.position, sightMask) { }

    public void StartWatching()
    {
        if (watchRoutine != null) return;
        watchRoutine = agent.StartCoroutine(WatchForItem());
    }

    public void StopWatching()
    {
        if (watchRoutine != null)
        {
            agent.StopCoroutine(watchRoutine);
            watchRoutine = null;
        }
        StopLookAt();
    }

    public void StartLookAt()
    {
        if (lookRoutine != null) return;
        lookRoutine = agent.StartCoroutine(LookAt(agent, getTargetPosition()));
    }

    public void StopLookAt()
    {
        if (lookRoutine == null) return;
        agent.StopCoroutine(lookRoutine);
        lookRoutine = null;
        StopLooking(agent);
    }

    public bool CanSeeTarget()
    {
        Vector3 origin = agent.Head.position;
        Vector3 toItem = getTargetPosition() - origin;
        float dist = toItem.magnitude;

        if (dist > agent.LookSettings.MaxSightDistance) return false;
        if (dist < 0.01f) return true;

        if (Physics.Raycast(origin, toItem / dist, out RaycastHit hit, dist, sightMask, QueryTriggerInteraction.Ignore))
            return hit.distance + agent.LookSettings.ItemTolerance >= dist;

        return true;
    }

    IEnumerator WatchForItem()
    {
        var wait = new WaitForSeconds(agent.LookSettings.CheckInterval);
        while (true)
        {
            if (CanSeeTarget())
                StartLookAt();
            else
                StopLookAt();

            yield return wait;
        }
    }

    public static IEnumerator LookAt(NPC agent, Vector3 target)
    {
        LookAtIK lookAnim = agent.GetComponentInChildren<LookAtIK>();
        if (lookAnim != null)
            yield return agent.StartCoroutine(lookAnim.LookAt(target));
    }

    public static void StopLooking(NPC agent)
    {
        LookAtIK lookAnim = agent.GetComponentInChildren<LookAtIK>();
        if (lookAnim != null)
            agent.StartCoroutine(lookAnim.StopLooking());
    }
}