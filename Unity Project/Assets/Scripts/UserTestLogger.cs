using System;
using System.IO;
using UnityEngine;

public class UserTestLogger : MonoBehaviour
{
    [FolderPath]
    [SerializeField] string logFolder;

    string path;

    void Awake()
    {
        string fileName = $"user-test-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        string folder = string.IsNullOrWhiteSpace(logFolder) ? DefaultFolder() : logFolder;
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "timestamp,user_message,backend_response\n");
        Debug.Log("User test log written to " + path);
    }

    // Repo root / UserTestLogs in the Editor, otherwise the player save folder
    public static string DefaultFolder()
    {
#if UNITY_EDITOR
        string unityProject = Directory.GetParent(Application.dataPath).FullName;
        string repoRoot = Directory.GetParent(unityProject).FullName;
        return Path.Combine(repoRoot, "UserTestLogs");
#else
        return Application.persistentDataPath;
#endif
    }

    public void LogTurn(string userMessage, string backendResponse)
    {
        if (string.IsNullOrEmpty(path)) return;
        string line = $"{Quote(DateTime.Now.ToString("o"))},{Quote(userMessage)},{Quote(backendResponse)}\n";
        File.AppendAllText(path, line);
    }

    static string Quote(string value)
    {
        if (value == null) value = "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
