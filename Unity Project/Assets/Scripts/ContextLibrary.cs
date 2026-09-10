using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

public class ContextLibrary : MonoBehaviour
{
    [NoFoldout] public List<ContextItem> spots = new();
    [NoFoldout] public List<ContextItem> environment = new();

    public NavMeshAgent agent;

    public uint maxMessageHistory = 10;
    private readonly LinkedList<string> messageHistory = new();

    public void AddMessageToHistory(string message)
    {
        messageHistory.AddLast(message);
        if (messageHistory.Count > maxMessageHistory)
        {
            messageHistory.RemoveFirst();
        }
    }

    public string GetContext(ContextResponse query)
    {
        string context = "";
        if (query.getSpots) context = GetSpotsContext(context);

        context += $"\nThis is your NPC data: {GetItemData(new ContextItem { name = "Agent", transform = agent.transform }, true, true, true)}";
        if (messageHistory.Count > 0)
        {
            context += "\nThese are past messages from user: ";
            foreach (var msg in messageHistory)
            {
                context += $"\n- {msg}";
            }
        }
        return context;
    }

    public string GetItemData(ContextItem item, bool includePosition, bool includeRotation, bool includeBounds)
    {
        List<string> dataParts = new();

        dataParts.Add($"name: {item.name}");
        if (!string.IsNullOrEmpty(item.description)) dataParts.Add($"description: {item.description}");

        if (includePosition)
        {
            dataParts.Add($"position: {item.transform.position}");
        }

        if (includeRotation)
        {
            dataParts.Add($"rotation: {item.transform.eulerAngles}");
        }

        if (includeBounds)
        {
            item.RecalculateBounds();

            dataParts.Add($"boundsCenter: {item.boundingBox.center}");
            dataParts.Add($"boundsSize: {item.boundingBox.size}");
        }

        return $"{{{string.Join(", ", dataParts)}}}";
    }

    string GetSpotsContext(string result)
    {
        result += "These are all the spot positions in the digital world: ";
        string[] spotJsons = new string[this.spots.Count];
        for (int i = 0; i < spots.Count; i++)
        {
            var spot = spots[i];
            spotJsons[i] = GetItemData(spot, true, false, false);
        }
        return $"{result}[{string.Join(',', spotJsons)}]";
    }
}