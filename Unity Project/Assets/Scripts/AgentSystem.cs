using System;
using UnityEngine;

[Serializable]
public class ActionData
{
    public string name;
    public string[] parameters;
}

[Serializable]
public class ActionResponse
{
    public ActionData[] actions;
}

[Serializable]
public class ContextResponse
{
    public bool getSpots;
    public UserContext getUser;
    public ObjectContext getObjects;

    public static ContextResponse GetFullContext()
    {
         return new ContextResponse
        {
            getSpots = true,
            getUser = new UserContext
            {
                position = true,
                rotation = true,
                neighboiurs = true
            },
            getObjects = new ObjectContext
            {
                position = true,
                rotation = true,
                description = true,
                neighboiurs = true
            }
        };
    }
}

[Serializable]
public class UserContext
{
    public bool position;
    public bool rotation;
    public bool neighboiurs;
}

[Serializable]
public class ObjectContext
{
    public bool position;
    public bool rotation;
    public bool description;
    public bool neighboiurs;
}


public class AgentSystem : MonoBehaviour
{
    [SerializeField] LLM llm;
    [SerializeField] AnimationLibrary animationLibrary;
    public bool sendAllContext = false;
    [field: SerializeField] public ContextLibrary contextLibrary { get; private set; }
    [field: SerializeField] public ChatManager chatManager { get; private set; }

    // probably need to add a way to add more actions to this list in the future, but for now, we'll hardcode them here.
    private string systemPrompt = @"
You are an NPC in a digital world. You will be given context about the world and the user will ask you tasks/questions related to the context.
Below are the actions you can perform to achieve the task / answer the question asked by the user along with documentation about when to use it. 
Action List:
// when user asks you to move to a specific spot in the world, use this action. The parameter is the name of the spot.
moveToSpot(spotName: string)
// when the user asks you to go to an arbitrary point in the world, use this action. The parameters are the x, y, and z coordinates of the point.
moveToPoint(x: float, y: float, z: float)
// use this action when the user asks you something or where a comment to some other action is appropriate. The parameter is the text you want to say.
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
      ""name"": ""moveToPoint"",
      ""parameters"": [1, 2, 3]
    },
    {
      ""name"": ""talk"",
      ""parameters"": [""Hello, how are you?""]
    }
  ]
}";

    private string contextString = @"
You need to provide what context of the world is needed to perform the action asked by the user, so a processing algorithm can efficiently work with only the needed data. 
You MUST respond ONLY with a valid JSON object with all the fields from the schema in the exact format shown below.
Format:
{
    ""getSpots:"" true,
    ""getUser"": {
        ""position"": true,
        ""rotation"": false,
        ""neighboiurs"": false
    },
...
}
Full Schema:
{
    getSpots: bool,
    getUser: {
        position: bool,
        rotation: bool,
        neighboiurs: bool
    },
    getObjects: {
        position: bool,
        rotation: bool,
        description: bool
        neighboiurs: bool
    }
}
";

    private void Awake()
    {
        chatManager.OnTextSent.AddListener(OnUserMessage);
    }

    private void OnUserMessage(string txt)
    {
        if (sendAllContext)
        {
            GetActionsJson(txt, ContextResponse.GetFullContext());
        }
        else
        {
            GetContextJson(txt);
        }
        contextLibrary.AddMessageToHistory(txt);
    }

    private string ExtractJson(string input)
    {
        int startIndex = input.IndexOf('{');
        int endIndex = input.LastIndexOf('}');

        if (startIndex != -1 && endIndex != -1 && endIndex > startIndex)
        {
            return input.Substring(startIndex, endIndex - startIndex + 1);
        }

        return input;
    }

    private void GetContextJson(string userPrompt)
    {
        Debug.Log($"Sending to LLM Round 1: \n{contextString}\n{userPrompt}");

        llm.SendChatMessage(userPrompt, contextString,
            onSuccess: (str) =>
            {
                Debug.Log($"LLM Response: \n{str}");

                try
                {
                    string cleanJson = ExtractJson(str);

                    ContextResponse response = JsonUtility.FromJson<ContextResponse>(cleanJson);
                    GetActionsJson(userPrompt, response);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse JSON. Error: {e.Message}\nRaw Output: {str}");
                }
            },
            onError: (str) => chatManager.AddChat(str)
        );
    }

    void GetActionsJson(string userPrompt, ContextResponse context)
    {
        //string txt = $"\n\nUser request: {userPrompt}\n{actionText}";
        string system = $"{systemPrompt}\n\nContext:\n{contextLibrary.GetContext(context)}";
        Debug.Log($"Sending to LLM Round 2: \nSystemText:\n{system}\nUserText:\n{userPrompt}");

        //llm.SendChatMessage($"User request: {userPrompt}", systemPrompt,
        llm.SendChatMessage(userPrompt, system, 
            onSuccess: (str) =>
            {
                Debug.Log($"LLM Response: \n{str}");

                try
                {
                    string cleanJson = ExtractJson(str);

                    ActionResponse response = JsonUtility.FromJson<ActionResponse>(cleanJson);

                    if (response == null || response.actions == null)
                    {
                        Debug.LogError("Parsed JSON was empty or missing the 'actions' array.");
                        return;
                    }
                    foreach (var action in response.actions)
                    {
                        string[] parameters = action.parameters ?? new string[0];

                        string error = animationLibrary.PlayAnimation(this, action.name, parameters);

                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.Log(error);
                            chatManager.AddChat(error);
                        }
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
}