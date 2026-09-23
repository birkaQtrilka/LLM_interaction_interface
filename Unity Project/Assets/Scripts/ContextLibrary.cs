using System.Collections.Generic;
using UnityEngine;

public struct ItemDataQuery
{
    public bool inclPosition;
    public bool inclRotation;
    public bool inclBounds;
    public bool inclNeighbors;
    public bool inclDescription;
    public ItemDataQuery(bool includePosition, bool includeRotation, bool includeBounds, bool includeNeighbors, bool includeDescription)
    {
        this.inclPosition = includePosition;
        this.inclRotation = includeRotation;
        this.inclBounds = includeBounds;
        this.inclNeighbors = includeNeighbors;
        this.inclDescription = includeDescription;
    }
}

public class ContextLibrary : MonoBehaviour
{
    [ DisplayFields("transform") ] public List<ContextItem> spots = new();
    [NoFoldout] public List<ContextItem> environment = new();
    [SerializeField] bool includeChatHistory = true;
    [SerializeField] bool includePlayingAnimations = true;
                                                    
    public NPC[] agents;

    public uint maxMessageHistory = 10;
    private readonly LinkedList<string> messageHistory = new();


    [Header("Debug Visuals")]
    public bool showGizmos = true;

    private void OnDrawGizmos()
    {
        if (!showGizmos || environment == null) return;

        foreach (ContextItem item in environment)
        {
            if (item == null || item.transform == null || item.boundingBox == new Bounds()) continue;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(item.boundingBox.center, item.boundingBox.size);

            Gizmos.color = Color.white;
            float n = item.neighborDistanceThreshold;
            Vector3 expandedSize = item.boundingBox.size + new Vector3(n, n, n);
            Gizmos.DrawWireCube(item.boundingBox.center, expandedSize);

            if (item.neighbors == null) continue;
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
        context = GetNpcContext(context);

        if(animations != null && animations.Count > 0 && includePlayingAnimations)
        {
            context += "These are all currently playing animations:";
            foreach (var anim in animations)
            {
                context += $"\n- {anim}";
            }
        }

        if (query.getSpots) context = GetSpotsContext(context);
        if (environment.Count > 0 && query.getObjects)
        {
            context += "\nThese are the environment objects: ";
            foreach (var obj in environment)
            {
                string objData = GetItemData(
                    obj,
                    new ItemDataQuery(
                        query.objectFlags.position,
                        query.objectFlags.rotation,
                        query.objectFlags.bounds,
                        query.objectFlags.neighbours,
                        query.objectFlags.description
                        )
                    );
                context += $"\n  {objData}";
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

    public string GetItemData(ContextItem item, ItemDataQuery q)
    {
        List<string> dataParts = new(10);

        GetItemData(dataParts, item, q);
        return $"{{{string.Join(", ", dataParts)}}}";
    }

    public List<string> GetItemData(List<string> dataParts, ContextItem item, ItemDataQuery q)
    {
        dataParts.Add($"name: {item.GetName()}");

        if (q.inclPosition)
        {
            dataParts.Add($"position: {item.transform.position}");
        }

        if (q.inclRotation)
        {
            dataParts.Add($"rotation: {item.transform.eulerAngles}");
        }

        if (q.inclBounds)
        {
            item.RecalculateBounds();

            dataParts.Add($"boundsCenter: {item.boundingBox.center}");
            dataParts.Add($"boundsSize: {item.boundingBox.size}");
        }

        if (q.inclNeighbors)
        {
            if (!q.inclBounds) item.RecalculateBounds();
            try
            {
                item.FindNeighbors();

            }catch(System.Exception e)
            {
                Debug.LogError(e);
                Debug.LogError("Error on item: " + item.transform);
            }
            List<string> neighborNames = new();
            foreach (var neighbor in item.neighbors)
            {
                neighborNames.Add(neighbor.name);
            }
            dataParts.Add($"neighbors: [{string.Join(", ", neighborNames)}]");
        }
        if (!string.IsNullOrEmpty(item.description)) dataParts.Add($"description: {item.description}");

        return dataParts ;
    }

    string GetSpotsContext(string result)
    {
        result += "These are walk spots: ";
        string[] spotJsons = new string[this.spots.Count];
        for (int i = 0; i < spots.Count; i++)
        {
            var spot = spots[i];
            spotJsons[i] = GetItemData(spot, new ItemDataQuery(
                includePosition: true, 
                includeRotation: false, 
                includeBounds: false, 
                includeNeighbors: false,
                includeDescription: false
            ));
        }
        return $"{result}[{string.Join(',', spotJsons)}]";
    }

    string GetNpcContext(string context)
    {
        List<string> npcDataParts = new(10);
        List<string> npcs = new(2);
        foreach (var agent in agents)
        {
            GetItemData(
            npcDataParts,
            new ContextItem { transform = agent.transform },
            new ItemDataQuery(
                includePosition: true,
                includeRotation: true,
                includeBounds: true,
                includeNeighbors: false,
                includeDescription: true
            )
        );
            Transform rightHandItem = agent.GetItem(right: true);
            npcDataParts.Add($"itemInRightHand: {(rightHandItem == null ? "None" : rightHandItem.name)}");
            npcs.Add( $"{{{string.Join(", ", npcDataParts)}}}");
            npcDataParts.Clear();
        }

        
        context += $"\nThese are the agents: [{string.Join(", ", npcs)}]\n";
        return context ;
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