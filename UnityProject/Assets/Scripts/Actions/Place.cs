using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Place : IAgentAction
{
    public string Name => "place";

    // builds the action to later be added in a queue
    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length != 1) return "place requires 1 string parameter: target object name";

        List<ContextItem> environment = context.contextLibrary.environment;
        ContextItem obj = context.GetObject(action.parameters[0]);
        if (obj == null) return $"Couldn't find object with name {action.parameters[0]}";

        Transform target = new GameObject("PlaceTarget").transform;

        IEnumerator place()
        {
            GrabbableItem held = agent.Hand.Held;
            if (held == null)
            {
                Debug.LogWarning("Agent tried to place an item but wasn't holding anything!");
                yield break;
            }

            ContextItem item = context.GetObject(held.name);
            if (item == null)
            {
                Debug.LogWarning("Agent tried to place an item but it wasn't in the environment list!");
                yield break;
            }

            PlaceOnTop(item, obj, context, agent, out Vector3 placePos, out Quaternion placeRot);

            // Aim the hand so the item pivot ends up at placePos, not the grip point
            Transform grip = held.GripTarget;
            Vector3 gripLocal = held.transform.InverseTransformPoint(grip.position);
            target.SetPositionAndRotation(
                placePos + placeRot * gripLocal,
                placeRot * Quaternion.Inverse(held.transform.rotation) * grip.rotation);

            yield return MovementActions.EnsureFacing(agent, placePos);

            yield return agent.Hand.ReleaseAt(target, tr =>
            {
                tr.SetPositionAndRotation(placePos, placeRot);
                item.RecalculateBounds();
            });
        }

        void end()
        {
            if (target != null) GameObject.Destroy(target.gameObject);
        }

        result = new AnimAction(action, null, place(), end);
        return null;
    }

    /// <summary>
    /// Places an item on the top surface of a target context object within arm reach, avoiding its neighbors.
    /// </summary>
    public static Vector3 PlaceOnTop(ContextItem item, ContextItem targetObj, AgentSystem context)
    {
        PlaceOnTop(item, targetObj, context, null, out Vector3 finalPos, out _);
        return finalPos;
    }

    public static void PlaceOnTop(ContextItem item, ContextItem targetObj, AgentSystem context, NPC agent, out Vector3 finalPos, out Quaternion finalRot)
    {
        finalPos = default;
        finalRot = Quaternion.identity;

        targetObj.RecalculateBounds();
        targetObj.FindNeighbors();

        if (targetObj.boundingBox == new Bounds())
        {
            Debug.LogError("Target object has no bounding box to place an item on");
            return;
        }

        Quaternion savedItemRot = item.transform.rotation;
        item.transform.rotation = Quaternion.identity;
        item.RecalculateBounds();
        Bounds unrotatedItemBounds = item.boundingBox;
        item.transform.rotation = savedItemRot;

        float itemRadius = Mathf.Max(unrotatedItemBounds.extents.x, unrotatedItemBounds.extents.z);
        float itemHeightOffset = unrotatedItemBounds.extents.y;

        Vector3 gripLocal = Vector3.zero;
        if (item.transform.TryGetComponent<GrabbableItem>(out var grabbable) && grabbable.GripTarget != null)
            gripLocal = item.transform.InverseTransformPoint(grabbable.GripTarget.position);

        Collider[] targetColliders = targetObj.transform.GetComponentsInChildren<Collider>();
        GrabIK ik = agent != null ? (agent.Hand != null && agent.Hand.IK != null ? agent.Hand.IK : agent.GetComponentInChildren<GrabIK>()) : null;

        float minX = targetObj.boundingBox.min.x + itemRadius;
        float maxX = targetObj.boundingBox.max.x - itemRadius;
        if (minX > maxX) minX = maxX = targetObj.boundingBox.center.x;

        float minZ = targetObj.boundingBox.min.z + itemRadius;
        float maxZ = targetObj.boundingBox.max.z - itemRadius;
        if (minZ > maxZ) minZ = maxZ = targetObj.boundingBox.center.z;

        float sampleMinX = minX;
        float sampleMaxX = maxX;
        float sampleMinZ = minZ;
        float sampleMaxZ = maxZ;

        if (ik != null && ik.UpperArm != null)
        {
            Vector3 shoulder = ik.UpperArm.position;
            float maxReach = ik.MaxReachDistance;
            float rMinX = Mathf.Max(minX, shoulder.x - maxReach);
            float rMaxX = Mathf.Min(maxX, shoulder.x + maxReach);
            float rMinZ = Mathf.Max(minZ, shoulder.z - maxReach);
            float rMaxZ = Mathf.Min(maxZ, shoulder.z + maxReach);

            if (rMinX <= rMaxX && rMinZ <= rMaxZ)
            {
                sampleMinX = rMinX;
                sampleMaxX = rMaxX;
                sampleMinZ = rMinZ;
                sampleMaxZ = rMaxZ;
            }
        }

        bool foundReachableSpot = false;
        bool foundClearFallback = false;
        float bestFallbackReachDist = float.PositiveInfinity;
        Vector3 bestFallbackPos = default;
        Quaternion bestFallbackRot = Quaternion.identity;

        float edgeMargin = Mathf.Min(itemRadius * 0.5f, 0.05f);
        int maxAttempts = 50;
        for (int i = 0; i < maxAttempts; i++)
        {
            bool useReachBox = i < 35;
            float randX = useReachBox ? Random.Range(sampleMinX, sampleMaxX) : Random.Range(minX, maxX);
            float randZ = useReachBox ? Random.Range(sampleMinZ, sampleMaxZ) : Random.Range(minZ, maxZ);

            if (!TrySampleTopSurface(targetColliders, targetObj.boundingBox, randX, randZ, out Vector3 surfacePoint, out Vector3 surfaceNormal))
                continue;

            bool footprintOnSurface =
                TrySampleTopSurface(targetColliders, targetObj.boundingBox, randX + edgeMargin, randZ, out _, out _) &&
                TrySampleTopSurface(targetColliders, targetObj.boundingBox, randX - edgeMargin, randZ, out _, out _) &&
                TrySampleTopSurface(targetColliders, targetObj.boundingBox, randX, randZ + edgeMargin, out _, out _) &&
                TrySampleTopSurface(targetColliders, targetObj.boundingBox, randX, randZ - edgeMargin, out _, out _);

            if (useReachBox && !footprintOnSurface)
                continue;

            Quaternion candidateRot = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            Vector3 candidatePos = surfacePoint + surfaceNormal * itemHeightOffset;

            if (IsOverlappingNeighbors(candidatePos, surfacePoint.y, itemRadius, itemHeightOffset, item, targetObj, context))
                continue;

            Vector3 gripWorldPos = candidatePos + candidateRot * gripLocal;
            bool inReach = ik == null || ik.IsWithinReach(gripWorldPos);

            if (inReach)
            {
                finalPos = candidatePos;
                finalRot = candidateRot;
                foundReachableSpot = true;
                break;
            }

            float reachDist = ik != null ? ik.GetReachDistance(gripWorldPos) : 0f;
            if (reachDist < bestFallbackReachDist)
            {
                bestFallbackReachDist = reachDist;
                bestFallbackPos = candidatePos;
                bestFallbackRot = candidateRot;
                foundClearFallback = true;
            }
        }

        if (!foundReachableSpot)
        {
            if (foundClearFallback)
            {
                finalPos = bestFallbackPos;
                finalRot = bestFallbackRot;
            }
            else
            {
                Debug.LogWarning($"Surface of {targetObj.GetName()} is too crowded or unreachable! Placing on closest surface point.");
                Vector3 refPoint = ik != null && ik.UpperArm != null ? ik.UpperArm.position : targetObj.boundingBox.center;
                float clampedX = Mathf.Clamp(refPoint.x, minX, maxX);
                float clampedZ = Mathf.Clamp(refPoint.z, minZ, maxZ);

                if (!TrySampleTopSurface(targetColliders, targetObj.boundingBox, clampedX, clampedZ, out Vector3 surfacePoint, out Vector3 surfaceNormal) &&
                    !TrySampleTopSurface(targetColliders, targetObj.boundingBox, targetObj.boundingBox.center.x, targetObj.boundingBox.center.z, out surfacePoint, out surfaceNormal))
                {
                    surfacePoint = new Vector3(targetObj.boundingBox.center.x, targetObj.boundingBox.max.y, targetObj.boundingBox.center.z);
                    surfaceNormal = Vector3.up;
                }

                finalRot = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
                finalPos = surfacePoint + surfaceNormal * itemHeightOffset;
            }
        }

        if (item.transform.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    static bool IsOverlappingNeighbors(Vector3 candidatePos, float surfaceY, float itemRadius, float itemHeightOffset, ContextItem item, ContextItem targetObj, AgentSystem context)
    {
        if (targetObj.neighbors == null) return false;

        Vector2 testPoint = new(candidatePos.x, candidatePos.z);
        foreach (Transform neighborRef in targetObj.neighbors)
        {
            if (neighborRef == null || neighborRef == item.transform || neighborRef == targetObj.transform) continue;
            if (neighborRef.TryGetComponent<GrabbableItem>(out var grabbableNeighbor) && grabbableNeighbor.IsHeld) continue;

            ContextItem neighbor = context.GetObject(neighborRef.name);
            if (neighbor == null) continue;

            neighbor.RecalculateBounds();
            if (neighbor.boundingBox.max.y <= surfaceY + 0.01f ||
                neighbor.boundingBox.min.y >= candidatePos.y + itemHeightOffset)
                continue;

            Vector2 neighborPos = new(neighbor.boundingBox.center.x, neighbor.boundingBox.center.z);
            float neighborRadius = Mathf.Max(neighbor.boundingBox.extents.x, neighbor.boundingBox.extents.z);

            if (Vector2.Distance(testPoint, neighborPos) < (itemRadius + neighborRadius))
                return true;
        }

        return false;
    }

    static bool TrySampleTopSurface(Collider[] colliders, Bounds bounds, float x, float z, out Vector3 surfacePoint, out Vector3 surfaceNormal)
    {
        surfacePoint = default;
        surfaceNormal = Vector3.up;

        if (colliders == null || colliders.Length == 0)
        {
            surfacePoint = new Vector3(x, bounds.max.y, z);
            return true;
        }

        float rayOriginY = bounds.max.y + 1f;
        float maxDistance = bounds.size.y + 2f;
        Ray ray = new Ray(new Vector3(x, rayOriginY, z), Vector3.down);

        bool found = false;
        float bestY = float.NegativeInfinity;

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled || col.isTrigger) continue;
            if (col.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                if (hit.normal.y >= 0.5f && hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    surfacePoint = hit.point;
                    surfaceNormal = hit.normal.normalized;
                    found = true;
                }
            }
        }

        return found;
    }
}