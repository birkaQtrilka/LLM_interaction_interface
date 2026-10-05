using System;
using System.Collections;
using System.Net.Http;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LLMBackend : MonoBehaviour
{
    [Serializable]
    class ContextRequestBody
    {
        public string message;
        public string session_id;
        public string log_directory;
        public string description;
    }

    [Serializable]
    class ActionsRequestBody
    {
        public string message;
        public string world;
        public string session_id;
        public string log_directory;
        public string description;
    }

    [Serializable]
    class SessionRequestBody
    {
        public string session_id;
        public string log_directory;
        public string description;
    }

    [Serializable]
    class FeedbackRequestBody
    {
        public string session_id;
        public int natural;
        public string natural_note;
        public int accurate;
        public string accurate_note;
        public string log_directory;
        public string description;
    }

    [Serializable]
    class ErrorRequestBody
    {
        public string session_id;
        public string message;
        public string stack;
        public string log_directory;
        public string description;
    }

    public string baseUrl = "http://127.0.0.1:8000";
    // Stored in the session file
    [SerializeField] string sessionDescription;
    // Empty uses Backend/logs, a short name is created there, and a full path is used as written
    [SerializeField] string logDirectory;
    string sessionId;
    bool reportingError;

    void OnEnable()
    {
        Application.logMessageReceived += OnUnityLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= OnUnityLog;
    }

    void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        if (!Application.isPlaying) return;
        if (type != LogType.Error && type != LogType.Exception) return;
        if (string.IsNullOrEmpty(sessionId) || reportingError) return;

        reportingError = true;
        try
        {
            string error = PostError(condition, stackTrace);
            if (error != null)
                Debug.LogWarning("Could not write the error to the session log: " + error);
        }
        finally
        {
            reportingError = false;
        }
    }

    void Start()
    {
        sessionId = Guid.NewGuid().ToString();
        string json = JsonUtility.ToJson(SessionBody());
        StartCoroutine(PostJson("/v1/session/start", json, null, null));
    }

    // Play Mode exit stops coroutines, so this call has to finish here.
    void OnDestroy()
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(2);
                string json = JsonUtility.ToJson(SessionBody());
                using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    client.PostAsync(baseUrl.TrimEnd('/') + "/v1/session/end", content).GetAwaiter().GetResult();
                }
            }
        }
        catch (Exception)
        {
        }
    }
    
    // Runs to completion on the click, so stopping Play just after Confirm still keeps the form
    public string SubmitFeedback(int natural, string naturalNote, int accurate, string accurateNote)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);
                var body = new FeedbackRequestBody
                {
                    session_id = sessionId ?? "",
                    log_directory = logDirectory ?? "",
                    description = sessionDescription ?? "",
                    natural = natural,
                    natural_note = naturalNote ?? "",
                    accurate = accurate,
                    accurate_note = accurateNote ?? "",
                };
                string json = JsonUtility.ToJson(body);
                using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage response = client.PostAsync(baseUrl.TrimEnd('/') + "/v1/session/feedback", content).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        string detail = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        return $"Backend error: {(int)response.StatusCode} {detail}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return null;
    }

    string PostError(string message, string stack)
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(2);
                var body = new ErrorRequestBody
                {
                    session_id = sessionId ?? "",
                    log_directory = logDirectory ?? "",
                    description = sessionDescription ?? "",
                    message = message ?? "",
                    stack = stack ?? "",
                };
                string json = JsonUtility.ToJson(body);
                using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage response = client.PostAsync(baseUrl.TrimEnd('/') + "/v1/session/error", content).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        string detail = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        return $"Backend error: {(int)response.StatusCode} {detail}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
        return null;
    }

    public void GetContext(string message, Action<ContextQuery> onSuccess, Action<string> onError = null)
    {
        string json = JsonUtility.ToJson(new ContextRequestBody { message = message, session_id = sessionId ?? "", log_directory = logDirectory ?? "", description = sessionDescription ?? "" });
        StartCoroutine(PostJson("/v1/context", json, text =>
        {
            try
            {
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
        string json = JsonUtility.ToJson(new ContextRequestBody { message = message, session_id = sessionId ?? "", log_directory = logDirectory ?? "", description = sessionDescription ?? "" });
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
        string json = JsonUtility.ToJson(new ActionsRequestBody { message = message, world = world, session_id = sessionId ?? "", log_directory = logDirectory ?? "", description = sessionDescription ?? "" });
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
        string json = JsonUtility.ToJson(new ActionsRequestBody { message = message, world = world, session_id = sessionId ?? "", log_directory = logDirectory ?? "", description = sessionDescription ?? "" });
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

    SessionRequestBody SessionBody()
    {
        return new SessionRequestBody
        {
            session_id = sessionId ?? "",
            log_directory = logDirectory ?? "",
            description = sessionDescription ?? "",
        };
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
