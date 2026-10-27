
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BatchFileRenamerWindow : EditorWindow
{
    private DefaultAsset targetFolder;
    private string toRemove = "AS_Annefleur";
    private string toReplace = "AF";
    private bool includeSubdirectories = true;

    private Vector2 scrollPosition;

    [MenuItem("Tools/Batch File Renamer")]
    private static void ShowWindow()
    {
        GetWindow<BatchFileRenamerWindow>("Batch File Renamer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Batch File Renamer",
            EditorStyles.boldLabel);

        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Target Directory", targetFolder,
            typeof(DefaultAsset), false);

        toRemove = EditorGUILayout.TextField(
            "Text to Remove", toRemove);

        toReplace = EditorGUILayout.TextField(
            "Replacement Text", toReplace);

        includeSubdirectories = EditorGUILayout.Toggle(
            "Include Subdirectories", includeSubdirectories);

        string folder = targetFolder != null
            ? AssetDatabase.GetAssetPath(targetFolder)
            : null;

        if (string.IsNullOrEmpty(folder) ||
            !AssetDatabase.IsValidFolder(folder))
        {
            EditorGUILayout.HelpBox(
                "Select a folder inside Assets.",
                MessageType.Info);
            return;
        }

        if (string.IsNullOrEmpty(toRemove))
        {
            EditorGUILayout.HelpBox(
                "Enter text to remove.",
                MessageType.Warning);
            return;
        }

        SearchOption searchOption = includeSubdirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var files = Directory.GetFiles(folder, "*", searchOption)
            .Where(path => !path.EndsWith(
                ".meta", StringComparison.OrdinalIgnoreCase))
            .Select(path => path.Replace('\\', '/'))
            .Where(path =>
                Path.GetFileName(path).Contains(toRemove))
            .Select(path => new
            {
                Path = path,
                OldName = Path.GetFileName(path),
                NewName = Path.GetFileName(path)
                    .Replace(toRemove, toReplace)
            })
            .Where(x => x.OldName != x.NewName)
            .ToList();

        EditorGUILayout.LabelField(
            $"Files to rename: {files.Count}");

        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition, GUILayout.Height(220));

        foreach (var file in files)
        {
            EditorGUILayout.LabelField(file.OldName);
            EditorGUILayout.LabelField(
                "→ " + file.NewName,
                EditorStyles.boldLabel);
            EditorGUILayout.Space(3);
        }

        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(files.Count == 0))
        {
            if (GUILayout.Button("Rename Files", GUILayout.Height(30)))
            {
                RenameAssets(files.Select(x =>
                    (x.Path, x.NewName)).ToArray());
            }
        }
    }

    private void RenameAssets((string path, string newName)[] files)
    {
        if (!EditorUtility.DisplayDialog(
            "Confirm Rename",
            $"Rename {files.Length} assets using Unity's AssetDatabase?",
            "Rename", "Cancel"))
            return;

        int renamed = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var file in files)
            {
                if (!File.Exists(file.path))
                    continue;

                string error = AssetDatabase.RenameAsset(
                    file.path, file.newName);

                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError(
                        $"Could not rename '{file.path}': {error}");
                    continue;
                }

                renamed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Rename Finished",
            $"Successfully renamed {renamed} asset(s).\n" +
            "Check the Console for any errors.",
            "OK");
    }
}
