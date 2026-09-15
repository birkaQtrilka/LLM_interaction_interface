using UnityEngine;

[System.Serializable]
public class ContextItem
{
    public string name;
    public Transform transform;
    public string description;

    [HideInInspector]
    public Bounds boundingBox;

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
}
