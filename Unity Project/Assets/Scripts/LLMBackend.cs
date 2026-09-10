using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class BackendReply
{
    public string say;
    public ActionData[] actions;
}

public class LLMBackend : MonoBehaviour
{
    [Serializable]
    class TurnRequest
    {
        public string message;
        public string world;
    }

    [SerializeField] string baseUrl = "http://127.0.0.1:8000";
    [SerializeField] bool logJson;

    public void SendChatMessage(string message, string world, Action<BackendReply> onSuccess, Action<string> onError = null)
    {
        StartCoroutine(SendTurnRoutine(message, world, onSuccess, onError));
    }

    IEnumerator SendTurnRoutine(string message, string world, Action<BackendReply> onSuccess, Action<string> onError)
    {
        TurnRequest body = new TurnRequest { message = message, world = world };
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

        BackendReply response = JsonUtility.FromJson<BackendReply>(request.downloadHandler.text);
        if (response == null)
        {
            onError?.Invoke("Received empty or invalid response from backend.");
            yield break;
        }

        onSuccess?.Invoke(response);
    }
}
