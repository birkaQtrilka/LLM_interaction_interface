using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ContextItem
{
    public string name;
    public Transform transform;

}

public class ContextLibrary : MonoBehaviour
{
    [NoFoldout] public List<ContextItem> spots = new();

    public string GetContext()
    {
        string context = "";
        foreach (var spot in spots)
        {
            context += $"{spot.name}: {spot.transform.position}\n";
        }
        return context;
    }
}
