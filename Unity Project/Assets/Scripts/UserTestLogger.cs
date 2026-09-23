using System;
using System.IO;
using UnityEngine;

public class UserTestLogger : MonoBehaviour
{
    [FolderPath]
    [SerializeField] string logFolder = "TempLogs";

    string path;

    void Awake()
    {
        string fileName = $"user-test-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        string folder = Path.Combine(RepoRoot(), logFolder);
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "timestamp,user_message,backend_response\n");
        Debug.Log("User test log written to " + path);
    }

    public static string RepoRoot()
    {
        return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
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
