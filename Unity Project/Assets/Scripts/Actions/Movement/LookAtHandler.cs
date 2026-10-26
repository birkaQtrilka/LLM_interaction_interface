using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public struct LookSettings
{
    public float MaxSightDistance;
    public float CheckInterval;
    /// <summary>
    /// When the ray travels toward that point, it often hits the item’s own collider a little before reaching the exact point, 
    /// because the collider has some size around its pivot. If you required “nothing was hit before the target”, 
    /// the NPC would be blocked by the very item it is trying to look at, and would never see it.
    /// </summary>
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

    public LookAtHandler(NPC agent, Func<Vector3> getTargetPosition, LayerMask sightMask = default)
    {
        this.agent = agent;
        this.getTargetPosition = getTargetPosition;
        this.sightMask = sightMask == default ? ~0 : sightMask;
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

    public bool DebugDraw = true;

    public bool CanSeeTarget()
    {
        Vector3 origin = agent.Head.position;
        Vector3 toItem = getTargetPosition() - origin;
        float dist = toItem.magnitude;
        float maxDist = agent.LookSettings.MaxSightDistance;
        float tolerance = agent.LookSettings.ItemTolerance;
        float duration = agent.LookSettings.CheckInterval;

        if (dist > maxDist)
        {
            if (DebugDraw)
            {
                Vector3 d = toItem / dist;
                // Yellow up to max sight distance, grey beyond it
                Debug.DrawRay(origin, d * maxDist, Color.yellow, duration);
                Debug.DrawRay(origin + d * maxDist, d * (dist - maxDist), Color.gray, duration);
                DrawCross(origin + d * maxDist, 0.1f, Color.yellow, duration);
            }
            return false;
        }

        if (dist < 0.01f) return true;

        Vector3 dir = toItem / dist;
        bool didHit = Physics.Raycast(origin, dir, out RaycastHit hit, dist, sightMask, QueryTriggerInteraction.Ignore);
        bool visible = !didHit || hit.distance + tolerance >= dist;

        if (DebugDraw)
        {
            // The threshold point: a hit at or beyond this distance still counts as seeing the target
            Vector3 thresholdPoint = origin + dir * Mathf.Max(0f, dist - tolerance);

            if (didHit)
            {
                Color rayColor = visible ? Color.green : Color.red;
                Debug.DrawRay(origin, dir * hit.distance, rayColor, duration);
                // Remainder of the ray past the hit point
                Debug.DrawRay(hit.point, dir * (dist - hit.distance), Color.gray, duration);
                DrawCross(hit.point, 0.08f, Color.magenta, duration);
                Debug.DrawRay(hit.point, hit.normal * 0.3f, Color.blue, duration);
            }
            else
            {
                Debug.DrawRay(origin, dir * dist, Color.green, duration);
            }

            DrawCross(thresholdPoint, 0.06f, Color.cyan, duration);
            DrawCross(origin + dir * dist, 0.1f, visible ? Color.green : Color.red, duration);
        }

        return visible;
    }

    static void DrawCross(Vector3 p, float size, Color color, float duration)
    {
        Debug.DrawLine(p + Vector3.up * size, p + Vector3.down * size, color, duration);
        Debug.DrawLine(p + Vector3.left * size, p + Vector3.right * size, color, duration);
        Debug.DrawLine(p + Vector3.forward * size, p + Vector3.back * size, color, duration);
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