using System.Collections.Generic;
using UnityEngine;

public class Place : IAgentAction
{
    public string Name => "place";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length != 1) return "place requires 1 string parameter: target object name";

        var environment = context.contextLibrary.environment;
        var obj = environment.Find(x => x.GetName() == action.parameters[0]);
        if (obj == null) return $"Couldn't find object with name {action.parameters[0]}";
        //if( agent.GetItem(right: true) == null) return "Hand is empty";

        Flag hasReleased = new();

        void start()
        {
            agent.Anim.SetTrigger("Place");
            agent.GrabReceiver.OnGrabPoint += releaseItem;
        }

        void releaseItem()
        {
            Transform itemTr = agent.ReleaseItem(right: true);
            if (itemTr == null)
            {
                Debug.LogWarning("Agent tried to place an item but wasn't holding anything!");
                hasReleased.value = true;
                return;
            }
            var item = environment.Find(x => x.GetName() == itemTr.name);

            if (item != null)
                PlaceOnTop(item, obj, environment);
            else
                Debug.LogWarning("Agent tried to place an item but wasn't holding anything!");

            hasReleased.value = true;
        }

        void end()
        {
            agent.GrabReceiver.OnGrabPoint -= releaseItem;
        }

        result = new AnimAction(action, start, Utils.MonitorFlag(hasReleased), end);
        return null;
    }

    /// <summary>
    /// Places an item on the top surface of a target context object, avoiding its neighbors.
    /// </summary>
    static void PlaceOnTop(ContextItem item, ContextItem targetObj, List<ContextItem> environment)
    {
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
                    ContextItem neighbor = environment.Find(x => x.transform == neighborRef);
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

        item.transform.SetPositionAndRotation(new Vector3(finalPlacementPos.x, topY + itemHeightOffset, finalPlacementPos.z), Quaternion.identity);

        if (item.transform.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}