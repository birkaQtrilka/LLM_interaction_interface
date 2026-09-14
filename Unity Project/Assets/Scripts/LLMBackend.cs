using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LLMBackend : MonoBehaviour
{
    [Serializable]
    class TurnRequest
    {
        public string message;
    }

    [Serializable]
    class TurnResponse
    {
        public string say;
    }

    [SerializeField] string baseUrl = "http://127.0.0.1:8000";
    [SerializeField] ChatManager chatManager;
    [SerializeField] bool logJson;

    void Awake()
    {
        if (chatManager == null)
        {
            return;
        }

        chatManager.OnTextSent.AddListener(OnUserMessage);
    }

    void OnUserMessage(string message)
    {
        SendTurn(message, onSuccess: chatManager.AddChat, onError: chatManager.AddChat);
    }

    public void SendTurn(string message, Action<string> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(SendTurnRoutine(message, onSuccess, onError));
    }

    IEnumerator SendTurnRoutine(string message, Action<string> onSuccess, Action<string> onError)
    {
        TurnRequest body = new TurnRequest { message = message };
        byte[] postData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
        string url = baseUrl.TrimEnd('/') + "/v1/turn";

        if (logJson)
        {
            Debug.Log("Unity - backend: " + Encoding.UTF8.GetString(postData));
        }

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(postData);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 60;

        yield return request.SendWebRequest();

        if (logJson)
        {
            Debug.Log("backend - Unity: " + request.downloadHandler.text);
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            string errorMessage = $"Backend error: {request.error}\n{request.downloadHandler.text}";
            Debug.LogError(errorMessage);
            onError?.Invoke(errorMessage);
            yield break;
        }

        TurnResponse response = JsonUtility.FromJson<TurnResponse>(request.downloadHandler.text);
        if (response == null || string.IsNullOrEmpty(response.say))
        {
            onError?.Invoke("Received empty or invalid response from backend.");
            yield break;
        }

        onSuccess?.Invoke(response.say);
    }
}
