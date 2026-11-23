using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class LookHandle
{
    internal readonly Func<Vector3> Position;
    internal readonly bool RequireSight;

    internal LookHandle(Func<Vector3> position, bool requireSight)
    {
        Position = position;
        RequireSight = requireSight;
    }
}

/// <summary>
/// One per NPC. Owns the look target, line-of-sight checks and lingering.
/// Request() starts a look, Release() ends it with an optional linger.
/// During a linger any new Request takes over without the head blending out.
/// </summary>
public class LookController
{
    public bool DebugDraw = true;
    /// <summary>How long sight can be blocked before the head turns away (prevents flicker).</summary>
    public float SightGrace = 0.2f;

    readonly NPC npc;
    readonly List<LookHandle> handles = new List<LookHandle>();

    LookAtIK ik;
    Coroutine loop;
    bool engaged;

    // linger state
    Func<Vector3> lingerPosition;
    float lingerUntil;

    // sight state (belongs to the top handle)
    LookHandle sightOwner;
    float nextSightCheck, lastSeen;

    public LookController(NPC npc) { this.npc = npc; }

    public bool IsLooking => engaged;

    LookAtIK IK
    {
        get
        {
            if (ik == null) ik = npc.GetComponentInChildren<LookAtIK>();
            return ik;
        }
    }

    public LookHandle Request(Func<Vector3> position, bool requireSight = false)
    {
        if (IK == null || position == null) return null;

        var handle = new LookHandle(position, requireSight);
        handles.Add(handle);
        lingerPosition = null;                 // taking over any linger
        loop ??= npc.StartCoroutine(Run());
        return handle;
    }

    public void Release(LookHandle handle, float linger = 0f)
    {
        if (handle == null || !handles.Remove(handle)) return;
        if (handles.Count > 0) return;         // an earlier request is still active, fall back to it

        lingerPosition = handle.Position;
        lingerUntil = Time.time + Mathf.Max(0f, linger);
    }

    IEnumerator Run()
    {
        while (true)
        {
            Func<Vector3> source;
            LookHandle top = null;

            if (handles.Count > 0)
            {
                top = handles[handles.Count - 1];   // most recent request wins
                source = top.Position;
            }
            else if (lingerPosition != null && Time.time < lingerUntil)
            {
                source = lingerPosition;            // lingering: keep tracking, keep engagement as is
            }
            else break;

            if (!TryEvaluate(source, out Vector3 pos))
            {
                handles.Clear();                    // target destroyed
                break;
            }

            IK.SetTarget(pos);
            if (top != null)
                SetEngaged(!top.RequireSight || HasSight(top, pos));

            yield return null;
        }

        lingerPosition = null;
        sightOwner = null;
        SetEngaged(false);
        loop = null;
    }

    void SetEngaged(bool on)
    {
        if (engaged == on || IK == null) return;
        engaged = on;
        if (on) IK.Engage(npc.TurnDuration, npc.LookCurve);
        else IK.Disengage(npc.TurnDuration, npc.LookCurve);
    }

    static bool TryEvaluate(Func<Vector3> source, out Vector3 pos)
    {
        pos = default;
        try { pos = source(); return true; }
        catch (MissingReferenceException) { return false; }
    }

    // ---- Line of sight -------------------------------------------------

    bool HasSight(LookHandle owner, Vector3 target)
    {
        if (sightOwner != owner)
        {
            sightOwner = owner;
            nextSightCheck = 0f;
            lastSeen = float.NegativeInfinity;
        }

        if (Time.time >= nextSightCheck)
        {
            nextSightCheck = Time.time + npc.LookSettings.CheckInterval;
            if (CanSee(target)) lastSeen = Time.time;
        }
        return Time.time - lastSeen <= SightGrace;
    }

    bool CanSee(Vector3 target)
    {
        Vector3 origin = npc.Head.position;
        Vector3 toItem = target - origin;
        float dist = toItem.magnitude;
        float maxDist = npc.LookSettings.MaxSightDistance;
        float tolerance = npc.LookSettings.ItemTolerance;
        float duration = npc.LookSettings.CheckInterval;

        if (dist > maxDist)
        {
            if (DebugDraw)
            {
                Vector3 d = toItem / dist;
                Debug.DrawRay(origin, d * maxDist, Color.yellow, duration);
                Debug.DrawRay(origin + d * maxDist, d * (dist - maxDist), Color.gray, duration);
                DrawCross(origin + d * maxDist, 0.1f, Color.yellow, duration);
            }
            return false;
        }

        if (dist < 0.01f) return true;

        Vector3 dir = toItem / dist;
        bool didHit = Physics.Raycast(origin, dir, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore);
        bool visible = !didHit || hit.distance + tolerance >= dist;

        if (DebugDraw)
        {
            Vector3 thresholdPoint = origin + dir * Mathf.Max(0f, dist - tolerance);
            if (didHit)
            {
                Debug.DrawRay(origin, dir * hit.distance, visible ? Color.green : Color.red, duration);
                Debug.DrawRay(hit.point, dir * (dist - hit.distance), Color.gray, duration);
                DrawCross(hit.point, 0.08f, Color.magenta, duration);
                Debug.DrawRay(hit.point, hit.normal * 0.3f, Color.blue, duration);
            }
            else Debug.DrawRay(origin, dir * dist, Color.green, duration);

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
}