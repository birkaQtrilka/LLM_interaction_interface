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

            Vector3 placePos = PlaceOnTop(item, obj, context);
            Quaternion placeRot = Quaternion.identity;

            // Aim the hand so the item pivot ends up at placePos, not the grip point
            Transform grip = held.GripTarget;
            Vector3 gripLocal = held.transform.InverseTransformPoint(grip.position);
            target.SetPositionAndRotation(
                placePos + placeRot * gripLocal,
                placeRot * Quaternion.Inverse(held.transform.rotation) * grip.rotation);

            yield return agent.Hand.ReleaseAt(target, tr => tr.SetPositionAndRotation(placePos, placeRot));
        }

        void end()
        {
            if (target != null) GameObject.Destroy(target.gameObject);
        }

        result = new AnimAction(action, null, place(), end);
        return null;
    }

    /// <summary>
    /// Places an item on the top surface of a target context object, avoiding its neighbors.
    /// </summary>
    static Vector3 PlaceOnTop(ContextItem item, ContextItem targetObj, AgentSystem context)
    {
        if (targetObj.boundingBox == new Bounds())
        {
            Debug.LogError("Target object has no bounding box to place an item on");
            return new();
        }
        float topY = targetObj.boundingBox.max.y;
        float itemRadius = Mathf.Max(item.boundingBox.extents.x, item.boundingBox.extents.z);
        float itemHeightOffset = item.boundingBox.extents.y;

        Vector3 finalPlacementPos = targetObj.boundingBox.center;
        bool foundClearSpot = false;
        int maxAttempts = 30;

        for (int i = 0; i < maxAttempts; i++)
        {
            float randX = Random.Range(targetObj.boundingBox.min.x + itemRadius, targetObj.boundingBox.max.x - itemRadius);
            float randZ = Random.Range(targetObj.boundingBox.min.z + itemRadius, targetObj.boundingBox.max.z - itemRadius);

            Vector2 testPoint = new(randX, randZ);
            bool isOverlapping = false;

            if (targetObj.neighbors != null)
            {
                foreach (Transform neighborRef in targetObj.neighbors)
                {
                    ContextItem neighbor = context.GetObject(neighborRef.name);
                    if (neighbor == null) continue; // neighbor no longer tracked in environment

                    Vector2 neighborPos = new(neighbor.boundingBox.center.x, neighbor.boundingBox.center.z);
                    float neighborRadius = Mathf.Max(neighbor.boundingBox.extents.x, neighbor.boundingBox.extents.z);

                    if (Vector2.Distance(testPoint, neighborPos) < (itemRadius + neighborRadius))
                    {
                        isOverlapping = true;
                        break;
                    }
                }
            }

            if (!isOverlapping)
            {
                finalPlacementPos = new Vector3(randX, topY, randZ);
                foundClearSpot = true;
                break;
            }
        }

        if (!foundClearSpot)
        {
            Debug.LogWarning($"Surface of {targetObj.GetName()} is too crowded! Forcing placement at center.");
            finalPlacementPos = new Vector3(targetObj.boundingBox.center.x, topY, targetObj.boundingBox.center.z);
        }
        Vector3 finalPos = new (finalPlacementPos.x, topY + itemHeightOffset, finalPlacementPos.z);

        if (item.transform.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        return finalPos;
    }
}