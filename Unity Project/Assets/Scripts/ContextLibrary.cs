using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ContextLibrary : MonoBehaviour
{
    [NoFoldout] public List<ContextItem> spots = new();
    [NoFoldout] public List<ContextItem> environment = new();
    [SerializeField] bool includeChatHistory = true;
    [SerializeField] bool includePlayingAnimations = true;
                                                    
    public NPC agent;

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

    // TODO use the query to filter the context data returned
    public string GetContext(ContextQuery query, List<Animation> animations)
    {
        string context = "";
        if (query.getSpots) context = GetSpotsContext(context);

        context += $"\nThis is your NPC data: {GetItemData(new ContextItem { name = "Agent", transform = agent.transform }, true, true, true)}\n";

        if(animations != null && animations.Count > 0 && includePlayingAnimations)
        {
            context += "These are all currently playing animations:";
            foreach (var anim in animations)
            {
                context += $"\n- {anim}";
            }
        }

        if (environment.Count > 0)
        {
            context += "\nThese are the environment objects: ";
            foreach (var obj in environment)
            {
                context += $"\n  {GetItemData(obj, true, true, true)}";
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