using UnityEngine;

public readonly struct TransOrPos
{
    public readonly Transform transform;
    public readonly Vector3 position;
    public TransOrPos(Transform transform)
    {
        this.transform = transform;
        this.position = transform.position;
    }

    public TransOrPos(Vector3 position)
    {
        this.transform = null;
        this.position = position;
    }
    public bool IsTransform => transform != null;
}
