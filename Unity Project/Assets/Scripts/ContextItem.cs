using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ContextItem
{
    public Transform transform;
    public string description;
    public float neighborDistanceThreshold = 1f;

    public Transform[] neighbors;

    [HideInInspector] public Bounds boundingBox;


    // Optional: LayerMask to optimize physical overlap queries
    //public LayerMask neighborLayerMask = ~0;

    public string GetName() => transform.name;

    public void RecalculateBounds()
    {
        if (transform == null) return;

        // Note: Change 'Renderer' to 'Collider' if you want physics bounds instead.
        Renderer[] renderers = transform.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            boundingBox = new Bounds(transform.position, Vector3.zero);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        boundingBox = bounds;
    }

    public void FindNeighbors()
    {
        if (transform == null || neighborDistanceThreshold < 0) return;

        float n = neighborDistanceThreshold;
        Vector3 expandedSize = boundingBox.size + new Vector3(n, n, n);

        Vector3 halfExtents = expandedSize * 0.5f;

        Collider[] hits = Physics.OverlapBox(boundingBox.center, halfExtents, Quaternion.identity/*, neighborLayerMask*/);

        HashSet<Transform> validNeighbors = new();

        foreach (Collider hit in hits)
        {
            Transform root = FindBase(hit.transform);
            if (root == null || root == transform) continue;
            validNeighbors.Add(hit.transform);
        }

        neighbors = new Transform[validNeighbors.Count];
        validNeighbors.CopyTo(neighbors);
    }

    Transform FindBase(Transform t)
    {
        do
        {
            if (t.TryGetComponent<EnvironmentItemTag>(out _))
            {
                return t;
            }
            t = t.parent;
        } while (t.parent != null);
        return null;
    }
}