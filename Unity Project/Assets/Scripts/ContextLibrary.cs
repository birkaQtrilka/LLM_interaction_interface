using System.Collections.Generic;
using UnityEngine;

public class ContextLibrary : MonoBehaviour
{
    [ DisplayFields("transform") ] public List<ContextItem> spots = new();
    [NoFoldout] public List<ContextItem> environment = new();
    [SerializeField] bool includeChatHistory = true;
    [SerializeField] bool includePlayingAnimations = true;
                                                    
    public NPC agent;

    public uint maxMessageHistory = 10;
    private readonly LinkedList<string> messageHistory = new();


    [Header("Debug Visuals")]
    public bool showGizmos = true;

    private void OnDrawGizmos()
    {
        if (!showGizmos || environment == null) return;

        foreach (ContextItem item in environment)
        {
            if (item == null || item.transform == null) continue;

            // 1. Draw the Bounding Box
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(item.boundingBox.center, item.boundingBox.size);

            // Optional: Draw the expanded threshold box (where it looks for neighbors)
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f); // Faint cyan
            float n = item.neighborDistanceThreshold;
            Vector3 expandedSize = item.boundingBox.size + new Vector3(n, n, n);
            Gizmos.DrawWireCube(item.boundingBox.center, expandedSize);

            // 2. Draw lines to Neighbors
            if (item.neighbors != null)
            {
                Gizmos.color = Color.yellow;
                foreach (Transform neighbor in item.neighbors)
                {
                    if (neighbor != null)
                    {
                        // Draw line from the center of the bounding box to the neighbor
                        Gizmos.DrawLine(item.boundingBox.center, neighbor.position);

                        // Draw a small sphere at the neighbor's pivot to easily spot it
                        Gizmos.DrawSphere(neighbor.position, 0.1f);
                    }
                }
            }
        }
    }
    public void AddMessageToHistory(string message)
    {
        messageHistory.AddLast(message);
        if (messageHistory.Count > maxMessageHistory)
        {
            messageHistory.RemoveFirst();
        }
    }

    public string GetContext(ContextQuery query, List<Animation> animations)
    {
        string context = "";
        if (query.getSpots) context = GetSpotsContext(context);

        var npcData = GetItemData(
            new ContextItem { transform = agent.transform }, 
            includePosition:  true, 
            includeRotation:  true, 
            includeBounds:    true, 
            includeNeighbors: false
        );

        context += $"\nThis is your NPC data: {npcData}\n";

        if(animations != null && animations.Count > 0 && includePlayingAnimations)
        {
            context += "These are all currently playing animations:";
            foreach (var anim in animations)
            {
                context += $"\n- {anim}";
            }
        }

        if (environment.Count > 0 && query.getObjects)
        {
            context += "\nThese are the environment objects: ";
            foreach (var obj in environment)
            {
                context += $"\n  {GetItemData(obj, query.objectFlags.position, query.objectFlags.rotation, query.objectFlags.bounds, query.objectFlags.neighbours, includeDistance: query.objectFlags.position)}";
            }
        }

        if (messageHistory.Count > 0 && includeChatHistory)
        {
            context += "\nThese are past messages from user: ";
            foreach (var msg in messageHistory)
            {
                context += $"\n- {msg}";
            }
        }
        return context;
    }

    public string GetItemData(ContextItem item, bool includePosition, bool includeRotation, bool includeBounds, bool includeNeighbors, bool includeDistance = false)
    {
        List<string> dataParts = new()
        {
            $"name: {item.transform.name}"
        };
        if (!string.IsNullOrEmpty(item.description)) dataParts.Add($"description: {item.description}");

        if (includePosition)
        {
            dataParts.Add($"position: {item.transform.position}");
        }

        if (includeDistance && agent != null && item.transform != null)
        {
            float dist = Vector3.Distance(item.transform.position, agent.transform.position);
            dataParts.Add($"distanceFromNpc: {dist:F2}");
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

        if (includeNeighbors)
        {
            if(!includeBounds) item.RecalculateBounds();
            item.FindNeighbors();
            List<string> neighborNames = new();
            foreach (var neighbor in item.neighbors)
            {
                neighborNames.Add(neighbor.name);
            }
            dataParts.Add($"neighbors: [{string.Join(", ", neighborNames)}]");
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
            spotJsons[i] = GetItemData(spot, includePosition: true, includeRotation: false, includeBounds: false, includeNeighbors: false, includeDistance: true);
        }
        return $"{result}[{string.Join(',', spotJsons)}]";
    }

    [ContextMenu("Update Bounds and Neighbours")]
    public void UpdateBoundsAndNeighbours()
    {
        foreach (var item in environment)
        {
            item.RecalculateBounds();
            item.FindNeighbors();
        }
    }
}