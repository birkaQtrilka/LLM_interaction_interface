using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class ContextItem
{
    public string name;
    public Transform transform;
    public string description;
    public List<ContextItem> neighbors = new();


}

public class ContextLibrary : MonoBehaviour
{
    [NoFoldout] public List<ContextItem> spots = new();
    public NavMeshAgent agent;

    public uint maxMessageHistory = 10;
    LinkedList<string> messageHistory = new();

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
        if(query.getSpots) context = GetSpotsContext(context);

        context += $"\nThis is your NPC data: {GetItemData(new ContextItem { name = "Agent", transform = agent.transform}, true, true, false)}";
        if(messageHistory.Count > 0)
        {
            context += "\nThese are past messages from user: ";
            foreach (var msg in messageHistory)
            {
                context += $"\n- {msg}";
            }
        }
        return context;
    }

    public string GetItemData(ContextItem item, bool includePosition, bool includeRotation, bool includeNeighbors)
    {
        List<string> dataParts = new();

        dataParts.Add($"name: {item.name}");

        if (includePosition)
        {
            dataParts.Add($"position: {item.transform.position}");
        }

        if (includeRotation)
        {
            dataParts.Add($"rotation: {item.transform.rotation}");
        }

        if (includeNeighbors)
        {
            string neighborsString = string.Join(", ", item.neighbors.Select(n => n.name));

            dataParts.Add($"neighbors: [{neighborsString}]");
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
