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
    class MessageBody
    {
        public string message;
    }

    [Serializable]
    class TurnRequest
    {
        public string message;
        public string world;
    }

    [SerializeField] string baseUrl = "http://127.0.0.1:8000";
    [SerializeField] ChatManager chatManager;
    [SerializeField] bool logJson;

    public void RequestContext(string message, Action<ContextResponse> onSuccess, Action<string> onError = null)
    {
        string json = JsonUtility.ToJson(new MessageBody { message = message });
        StartCoroutine(PostJson("/v1/context", json, text =>
        {
            ContextResponse response = JsonUtility.FromJson<ContextResponse>(text);
            if (response == null)
            {
                onError?.Invoke("Received empty or invalid context from backend");
                return;
            }
            onSuccess?.Invoke(response);
        }, onError));
    }

    public void SendChatMessage(string message, string world, Action<BackendReply> onSuccess, Action<string> onError = null)
    {
        string json = JsonUtility.ToJson(new TurnRequest { message = message, world = world });
        StartCoroutine(PostJson("/v1/turn", json, text =>
        {
            BackendReply response = JsonUtility.FromJson<BackendReply>(text);
            if (response == null)
            {
                onError?.Invoke("Received empty or invalid response from backend");
                return;
            }
            onSuccess?.Invoke(response);
        }, onError));
    }

    IEnumerator PostJson(string path, string json, Action<string> onBody, Action<string> onError)
    {
        byte[] postData = Encoding.UTF8.GetBytes(json);
        string url = baseUrl.TrimEnd('/') + path;

        if (logJson)
        {
            Debug.Log("Unity - backend: " + json);
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

        onBody?.Invoke(request.downloadHandler.text);
    }
}
