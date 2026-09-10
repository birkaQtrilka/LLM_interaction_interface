using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class ContextItem
{
    public string name;
    public Transform transform;

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

        context += $"\nThis is your NPC data: position: {agent.transform.position}, rotation: {agent.transform.rotation}";
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

    public string GetItemData(ContextItem item, bool position, bool rotation)
    {
        return $"";
    }

    string GetSpotsContext(string result)
    {
        result += "These are all the spot positions in the digital world: ";
        for (int i = 0; i < spots.Count; i++)
        {
            var spot = spots[i];
            result += $"{spot.name}: {spot.transform.position}{(i == spots.Count - 1 ? "" : ", ")}";
        }
        return result;
    }
}
