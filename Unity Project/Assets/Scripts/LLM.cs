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

    [Header("Chat Settings")]
    [TextArea(3, 5)]
    [Tooltip("The system prompt gives the AI its personality and rules.")]
    //public string systemPrompt = "You are a helpful AI assistant in a Unity game.";
    [Range(0f, 2f)]
    public float temperature = 0.7f;

    //private const string OPENAI_URL = "https://api.openai.com/v1/chat/completions";// New Groq URL:
    private const string OPENAI_URL = "https://api.groq.com/openai/v1/chat/completions";

    private void Awake()
    {
        apiKey = LoadApiKey();
    }


    private string GetModelString()
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
        string fullPath = Path.Combine(Application.dataPath, apiKeyEnvPath);

        if (File.Exists(fullPath))
        {
            string keyText = File.ReadAllText(fullPath);

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
        OpenAIRequest requestData = new OpenAIRequest
        {
            model = GetModelString(),
            temperature = temperature,
            messages = new OpenAIMessage[]
            {
                //new OpenAIMessage { role = "system", content = systemPrompt },
                new OpenAIMessage { role = "user", content = userMessage }
            }
        };

        // Convert data to JSON string
        string jsonData = JsonUtility.ToJson(requestData);
        byte[] postData = Encoding.UTF8.GetBytes(jsonData);

        using UnityWebRequest request = new(OPENAI_URL, "POST");
        request.uploadHandler = new UploadHandlerRaw(postData);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            string errorMessage = $"Error communicating with OpenAI: {request.error}\nResponse: {request.downloadHandler.text}";
            Debug.LogError(errorMessage);

            onError?.Invoke(errorMessage);
        }
        else
        {
            string jsonResponse = request.downloadHandler.text;
            OpenAIResponse responseData = JsonUtility.FromJson<OpenAIResponse>(jsonResponse);

            if (responseData != null && responseData.choices != null && responseData.choices.Length > 0)
            {
                string reply = responseData.choices[0].message.content;

                onSuccess?.Invoke(reply);
            }
            else
            {
                string emptyErrorMessage = "Received empty or invalid response from OpenAI.";
                Debug.LogWarning(emptyErrorMessage);

                onError?.Invoke(emptyErrorMessage);
            }
        }
    }
    #region JSON Serialization Classes

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