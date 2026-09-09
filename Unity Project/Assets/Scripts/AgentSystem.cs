using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public class AgentSystem : MonoBehaviour
{
    [TextArea(3, 10)] string systemPrompt = "You are an NPC in a digital world. You will be given context about the world and the user will ask you tasks/questions related to the context.";
    [SerializeField] ChatManager chatManager;
    [SerializeField] LLM llm;
    [SerializeField] ContextLibrary contextLibrary;
    [SerializeField] AnimationLibrary animationLibrary;

    private string actionText = @"Bellow are the actions you can perform along with their parameters in order to achieve the task / answer the question asked by the user. 
Action List:
moveToSpot(spotName: string)
talk(msg: string)

You must respond in the following format: actionName: [param1, param2 ..], actionName: [param1 ..] ...";

    private void Awake()
    {
        chatManager.OnTextSent.AddListener(OnUserMessage);
    }

    private void OnUserMessage(string txt)
    {
        // currently there is no first stage and refined stage
        txt = $"{systemPrompt}\nContext:\n{contextLibrary.GetContext()}\n\nUser request: {txt}\n{actionText}";
        Debug.Log($"Sending to LLM: \n{txt}");
        llm.SendChatMessage(txt, 
            onSuccess: (str) =>
            {
                chatManager.AddChat(str);
                Debug.Log($"LLM Response: \n{str}");
                List<string[]> actions = Parse(str);
                foreach (var action in actions)
                {
                    string actionName = action[0];
                    string[] parameters = action.Skip(1).ToArray();
                    string error = animationLibrary.PlayAnimation(contextLibrary, actionName, parameters);
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.Log(error);
                        chatManager.AddChat(error);
                    }
                }
            },
            onError: (str) => chatManager.AddChat(str)
        );
    }


    public static List<string[]> Parse(string input)
    {
        var result = new List<string[]>();

        // REGEX EXPLANATION:
        // ([^:,]+)  -> Group 1: Matches the action name (anything that isn't a colon or comma)
        // \s*:\s*   -> Matches the colon, ignoring any spaces around it
        // \[([^\]]*)\] -> Group 2: Matches everything inside the [ ] brackets
        string pattern = @"([^:,]+)\s*:\s*\[([^\]]*)\]";

        foreach (Match match in Regex.Matches(input, pattern))
        {
            // 1. Get the action name and clean up any extra spaces
            string actionName = match.Groups[1].Value.Trim();

            // 2. Get the raw string of parameters from inside the brackets
            string paramsString = match.Groups[2].Value;

            // 3. Create a list to easily combine the action name and parameters
            var actionData = new List<string> { actionName };

            // 4. If there are parameters, split them by comma and add them
            if (!string.IsNullOrWhiteSpace(paramsString))
            {
                var parameters = paramsString.Split(',')
                                             .Select(p => p.Trim()); // Trim spaces off each parameter
                actionData.AddRange(parameters);
            }

            // 5. Convert the list to an array and add it to our final result
            result.Add(actionData.ToArray());
        }

        return result;
    }
}
