using System.Collections.Generic;
using UnityEngine;

public static partial class Actions
{
    public static AnimAction Place(NPC agent, ActionData action, List<ContextItem> environment)
    {
        Flag hasReleased = new();
        var obj = environment.Find(x => x.GetName() == action.parameters[0]);

        void start()
        {
            agent.Anim.SetTrigger("Place");
            agent.GrabReceiver.OnGrabPoint += releaseItem;
        }

        void releaseItem()
        {
            Transform itemTr = agent.ReleaseItem(right: true);
            var item = environment.Find(x => x.GetName() == itemTr.name);

            if (item != null)
            {
                PlaceOnTop(item, obj, environment);
            }
            else
            {
                Debug.LogWarning("Agent tried to place an item but wasn't holding anything!");
            }

            hasReleased.value = true;
        }

        void end()
        {
            agent.GrabReceiver.OnGrabPoint -= releaseItem;
        }



        return new AnimAction(action, start, Utils.MonitorFlag(hasReleased), end);
    }

    /// <summary>
    /// Places an item on the top surface of a target context object, avoiding its neighbors.
    /// </summary>
    static void PlaceOnTop(ContextItem item, ContextItem targetObj, List<ContextItem> environment)
    {
        float topY = targetObj.boundingBox.max.y;

        //Collider itemCol = item.GetComponent<Collider>();
        //if (itemCol == null)
        //{
        //    Debug.LogError("Error: didn't find collider on item: " + item.name);
        //    return;
        //} 
        // Fallback to 0.15f radius if the item doesn't have a collider
        //float itemRadius = itemCol != null ? Mathf.Max(itemCol.bounds.extents.x, itemCol.bounds.extents.z) : 0.15f;
        float itemRadius = Mathf.Max(item.boundingBox.extents.x, item.boundingBox.extents.z);

        float itemHeightOffset = item.boundingBox.extents.y;

        Vector3 finalPlacementPos = targetObj.boundingBox.center;
        bool foundClearSpot = false;
        int maxAttempts = 30;

        // 3. Try to find a clear spot on the surface
        for (int i = 0; i < maxAttempts; i++)
        {
            // Pick a random X and Z within the target's bounding box (padded by item radius to stay on edges)
            float randX = UnityEngine.Random.Range(targetObj.boundingBox.min.x + itemRadius, targetObj.boundingBox.max.x - itemRadius);
            float randZ = UnityEngine.Random.Range(targetObj.boundingBox.min.z + itemRadius, targetObj.boundingBox.max.z - itemRadius);

            Vector2 testPoint = new Vector2(randX, randZ);
            bool isOverlapping = false;

            // 4. Check against all neighbors on that object
            if (targetObj.neighbors != null)
            {
                foreach (Transform neighborRef in targetObj.neighbors)
                {
                    ContextItem neighbor = environment.Find(x => x.transform == neighborRef);

                    Vector2 neighborPos = new(neighbor.boundingBox.center.x, neighbor.boundingBox.center.z);
                    float neighborRadius = Mathf.Max(neighbor.boundingBox.extents.x, neighbor.boundingBox.extents.z);

                    // If distance between the two centers is less than both radii combined, they overlap
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

        // If the item has physics, zero out velocity so it doesn't fly away
        if (item.transform.TryGetComponent<Rigidbody>(out var rb))
        {
            //rb.isKinematic = false; // Make sure physics affects it again if it was disabled on grab
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
