using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LLMBackend : MonoBehaviour
{
    [Serializable]
    class ContextRequestBody
    {
        public string message;
    }

    [Serializable]
    class ActionsRequestBody
    {
        public string message;
        public string world;
    }

    public string baseUrl = "http://127.0.0.1:8000";
    
    public void GetContext(string message, Action<ContextQuery> onSuccess, Action<string> onError = null)
    {
        string json = JsonUtility.ToJson(new ContextRequestBody { message = message });
        StartCoroutine(PostJson("/v1/context", json, text =>
        {
            try
            {
                Debug.Log($"Received context from backend: {text}");
                ContextQuery response = JsonUtility.FromJson<ContextQuery>(text);
                onSuccess?.Invoke(response);
            }
            catch (Exception)
            {
                onError?.Invoke("Received invalid context from backend");
            }
        }, onError));
    }

    public IEnumerator GetContext(string message, CoroutineResult<ContextQuery> res)
    {
        string json = JsonUtility.ToJson(new ContextRequestBody { message = message });
        yield return StartCoroutine(PostJson("/v1/context", json, text =>
        {
            try
            {
                ContextQuery response = JsonUtility.FromJson<ContextQuery>(text);
                res.SetResult(response);
            }
            catch (Exception)
            {
                res.SetError("Received empty or invalid context from backend");
            }
        }, err =>
        {
            res.SetError(err);
        }));
    }

    public IEnumerator GetActions(string message, string world, CoroutineResult<ActionsResponse> res)
    {
        string json = JsonUtility.ToJson(new ActionsRequestBody { message = message, world = world });
        yield return StartCoroutine(PostJson("/v1/turn", json, text =>
        {
            ActionsResponse response = JsonUtility.FromJson<ActionsResponse>(text);
            if (response == null)
            {
                res.SetError("Received empty or invalid response from backend");
                return;
            }
            res.SetResult(response);
        }, err =>
        {
            res.SetError(err);
        }));
    }

    public void GetActions(string message, string world, Action<ActionsResponse> onSuccess, Action<string> onError = null)
    {
        string json = JsonUtility.ToJson(new ActionsRequestBody { message = message, world = world });
        StartCoroutine(PostJson("/v1/turn", json, text =>
        {
            ActionsResponse response = JsonUtility.FromJson<ActionsResponse>(text);
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

        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(postData);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.timeout = 60;

        yield return request.SendWebRequest();

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
