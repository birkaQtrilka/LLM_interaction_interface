using System;
using System.IO;
using UnityEngine;

public class UserTestLogger 
{
    string path;

    public UserTestLogger(string logFolder)
    {
        string fileName = $"user-test-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        string folder;
#if UNITY_EDITOR
        folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, logFolder);
#else
        folder = Application.persistentDataPath;
#endif
        Directory.CreateDirectory(folder);
        path = Path.Combine(folder, fileName);
        File.WriteAllText(path, "timestamp,user_message,backend_response\n");
        Debug.Log("User test log written to UserTestLogs/" + fileName);
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
