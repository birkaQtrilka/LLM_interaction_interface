using System.Collections.Generic;
using UnityEngine;
struct Pos
{
    public Vector3 position;
    public Quaternion rotation;
    public Pos(Vector3 pos, Quaternion rot)
    {
        position = pos;
        rotation = rot;
    }
}
public class ResetPos : MonoBehaviour
{
    public List<Transform> targets = new List<Transform>();
    List<Pos> originalPositions = new List<Pos>();

    private void Start()
    {
        SetAsDefault();
    }

    [ContextMenu("Position Back")]
    public void ResetPositions()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].SetPositionAndRotation(originalPositions[i].position, originalPositions[i].rotation);
        }
    }

    [ContextMenu("Set as default positions")]
    public void SetAsDefault()
    {
        originalPositions.Clear();
        for (int i = 0; i < targets.Count; i++)
        {
            originalPositions.Add( new Pos(targets[i].position, targets[i].rotation));
        }
    }
}
