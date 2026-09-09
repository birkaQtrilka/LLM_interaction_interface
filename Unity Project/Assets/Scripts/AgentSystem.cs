using System;
using UnityEngine;

[System.Serializable]
public class ActionData
{
    public string name;
    public string[] parameters;
}

[System.Serializable]
public class ActionResponse
{
    public ActionData[] actions;
}

public class AgentSystem : MonoBehaviour
{
    [TextArea(3, 10)] string systemPrompt = "You are an NPC in a digital world. You will be given context about the world and the user will ask you tasks/questions related to the context.";
    [SerializeField] ChatManager chatManager;
    [SerializeField] LLM llm;
    [SerializeField] ContextLibrary contextLibrary;
    [SerializeField] AnimationLibrary animationLibrary;

    private string actionText = @"Below are the actions you can perform to achieve the task / answer the question asked by the user. 
Action List:
moveToSpot(spotName: string)
talk(msg: string)

You MUST respond ONLY with a valid JSON object in the exact format shown below. Do not add any conversational text or markdown before or after the JSON.

Format:
{
  ""actions"": [
    {
      ""name"": ""moveToSpot"",
      ""parameters"": [""SpotA""]
    },
    {
      ""name"": ""talk"",
      ""parameters"": [""Hello, how are you?""]
    }
  ]
}";

    private void Awake()
    {
        chatManager.OnTextSent.AddListener(OnUserMessage);
    }

    private void OnUserMessage(string txt)
    {
        txt = $"{systemPrompt}\nContext:\n{contextLibrary.GetContext()}\n\nUser request: {txt}\n{actionText}";
        Debug.Log($"Sending to LLM: \n{txt}");

        llm.SendChatMessage(txt,
            onSuccess: (str) =>
            {
                Debug.Log($"LLM Response: \n{str}");

                try
                {
                    string cleanJson = ExtractJson(str);

                    ActionResponse response = JsonUtility.FromJson<ActionResponse>(cleanJson);

                    if (response != null && response.actions != null)
                    {
                        foreach (var action in response.actions)
                        {
                            // If parameters is null, default to an empty array to avoid null reference exceptions
                            string[] parameters = action.parameters ?? new string[0];

                            string error = animationLibrary.PlayAnimation(contextLibrary, action.name, parameters);

                            if (!string.IsNullOrEmpty(error))
                            {
                                Debug.Log(error);
                                chatManager.AddChat(error);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("Parsed JSON was empty or missing the 'actions' array.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse JSON. Error: {e.Message}\nRaw Output: {str}");
                }
            },
            onError: (str) => chatManager.AddChat(str)
        );
    }

    // Helper method to extract only the JSON object, ignoring extra text or markdown code blocks
    private string ExtractJson(string input)
    {
        int startIndex = input.IndexOf('{');
        int endIndex = input.LastIndexOf('}');

        if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
        {
            return input.Substring(startIndex, endIndex - startIndex + 1);
        }

        return input; // Fallback to the raw string if brackets aren't found
    }
}