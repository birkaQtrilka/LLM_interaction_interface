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

    public string GetContext()
    {
        string context = "These are all the spot positions in the digital world: ";
        for (int i = 0; i < spots.Count; i++)
        {
            var spot = spots[i];
            context += $"{spot.name}: {spot.transform.position}{(i == spots.Count - 1 ? "" : ", ")}";
        }
        context += $"\nThis is your NPC data: position: {agent.transform.position}";
        context += "\nThese are past messages from user: ";
        Debug.Log($"Context: \n{context}");
        return context;
    }
}
