using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LLM : MonoBehaviour
{
    [Header("API Settings")]
    [Tooltip("Enter your OpenAI API key here. Warning: Don't hardcode this in production games!")]
    public string apiKeyEnvPath = "";
    private string apiKey;

    [Tooltip("Select the ChatGPT model you want to use.")]
    public GptModel selectedModel = GptModel.GPT4oMini;

    [Header("Chat Settings")]
    [TextArea(3, 5)]
    [Tooltip("The system prompt gives the AI its personality and rules.")]
    public string systemPrompt = "You are a helpful AI assistant in a Unity game.";
    [Range(0f, 2f)]
    public float temperature = 0.7f;

    //private const string OPENAI_URL = "https://api.openai.com/v1/chat/completions";// New Groq URL:
    private const string OPENAI_URL = "https://api.groq.com/openai/v1/chat/completions";

    private void Awake()
    {
        apiKey = LoadApiKey();
    }

    public enum GptModel
    {
        [InspectorName("gpt-4o")] GPT4o,
        [InspectorName("gpt-4o-mini")] GPT4oMini,
        [InspectorName("gpt-4-turbo")] GPT4Turbo,
        [InspectorName("gpt-4")] GPT4,
        [InspectorName("gpt-3.5-turbo")] GPT35Turbo
    }

    //private string GetModelString(GptModel model)
    //{
    //    return model switch
    //    {
    //        GptModel.GPT4o => "gpt-4o",
    //        GptModel.GPT4oMini => "gpt-4o-mini",
    //        GptModel.GPT4Turbo => "gpt-4-turbo",
    //        GptModel.GPT4 => "gpt-4",
    //        GptModel.GPT35Turbo => "gpt-3.5-turbo",
    //        _ => "gpt-4o-mini",
    //    };
    //}
    private string GetModelString(GptModel model)
    {
        // Use Groq's current free model instead of the deprecated Llama one
        return "openai/gpt-oss-20b";
    }

    /// <summary>
    /// Call this method to send a prompt to ChatGPT.
    /// Passes the result to the onSuccess or onError callbacks.
    /// </summary>
    public void SendChatMessage(string userMessage, Action<string> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(SendRequestRoutine(userMessage, onSuccess, onError));
    }
    string LoadApiKey()
    {
        // 1. Construct the full file path (Application.dataPath = the "Assets" folder)
        string fullPath = Path.Combine(Application.dataPath, apiKeyEnvPath);

        // 2. Check if the file actually exists to avoid crashing
        if (File.Exists(fullPath))
        {
            // 3. Read the text inside the file
            string keyText = File.ReadAllText(fullPath);

            // .Trim() is highly recommended! It removes any invisible spaces or 
            // enter/return (newlines) you might have accidentally copied into the file.
            return keyText.Trim();
        }
        else
        {
            Debug.LogError($"Could not find the API key file at: {fullPath}");
            return "";
        }
    }

    private IEnumerator SendRequestRoutine(string userMessage, Action<string> onSuccess, Action<string> onError)
    {
        // 1. Prepare the Data
        OpenAIRequest requestData = new OpenAIRequest
        {
            model = GetModelString(selectedModel),
            temperature = temperature,
            messages = new OpenAIMessage[]
            {
                new OpenAIMessage { role = "system", content = systemPrompt },
                new OpenAIMessage { role = "user", content = userMessage }
            }
        };

        // Convert data to JSON string
        string jsonData = JsonUtility.ToJson(requestData);
        byte[] postData = Encoding.UTF8.GetBytes(jsonData);

        // 2. Setup the Web Request
        using UnityWebRequest request = new(OPENAI_URL, "POST");
        request.uploadHandler = new UploadHandlerRaw(postData);
        request.downloadHandler = new DownloadHandlerBuffer();

        // Set Headers
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        // 3. Send and Wait
        yield return request.SendWebRequest();

        // 4. Handle Response
        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            string errorMessage = $"Error communicating with OpenAI: {request.error}\nResponse: {request.downloadHandler.text}";
            Debug.LogError(errorMessage);

            // Trigger the error callback so the other script knows it failed
            onError?.Invoke(errorMessage);
        }
        else
        {
            // Parse the JSON response
            string jsonResponse = request.downloadHandler.text;
            OpenAIResponse responseData = JsonUtility.FromJson<OpenAIResponse>(jsonResponse);

            if (responseData != null && responseData.choices != null && responseData.choices.Length > 0)
            {
                string reply = responseData.choices[0].message.content;
                Debug.Log($"<color=green><b>ChatGPT ({GetModelString(selectedModel)}):</b></color> {reply}");

                // Trigger the success callback and pass the reply
                onSuccess?.Invoke(reply);
            }
            else
            {
                string emptyErrorMessage = "Received empty or invalid response from OpenAI.";
                Debug.LogWarning(emptyErrorMessage);

                // Trigger the error callback
                onError?.Invoke(emptyErrorMessage);
            }
        }
    }
    #region JSON Serialization Classes

    // Unity's JsonUtility requires plain classes with the [Serializable] attribute 
    // to properly convert to and from JSON.

    [Serializable]
    public class OpenAIRequest
    {
        public string model;
        public float temperature;
        public OpenAIMessage[] messages;
    }

    [Serializable]
    public class OpenAIMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    public class OpenAIResponse
    {
        public string id;
        public Choice[] choices;
    }

    [Serializable]
    public class Choice
    {
        public int index;
        public OpenAIMessage message;
        public string finish_reason;
    }

    #endregion
}