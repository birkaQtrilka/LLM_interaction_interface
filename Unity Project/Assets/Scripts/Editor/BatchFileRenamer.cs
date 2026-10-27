
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
    public static void ShowWindow()
    {
        GetWindow<BatchFileRenamerWindow>("Batch File Renamer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Batch File Renamer",
            EditorStyles.boldLabel
        );

        EditorGUILayout.Space(5);

        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Target Directory",
            targetFolder,
            typeof(DefaultAsset),
            false
        );

        toRemove = EditorGUILayout.TextField(
            "Text to Remove",
            toRemove
        );

        toReplace = EditorGUILayout.TextField(
            "Replacement Text",
            toReplace
        );

        includeSubdirectories = EditorGUILayout.Toggle(
            "Include Subdirectories",
            includeSubdirectories
        );

        EditorGUILayout.Space(10);

        string folderPath = GetFolderPath();

        if (string.IsNullOrEmpty(folderPath))
        {
            EditorGUILayout.HelpBox(
                "Select a folder inside your Unity project.",
                MessageType.Info
            );
            return;
        }

        if (string.IsNullOrEmpty(toRemove))
        {
            EditorGUILayout.HelpBox(
                "Enter the text you want to remove.",
                MessageType.Warning
            );
            return;
        }

        string[] files = GetFiles(folderPath);

        var renameOperations = files
            .Select(path => new
            {
                OldPath = path,
                OldName = Path.GetFileName(path),
                Directory = Path.GetDirectoryName(path)
            })
            .Where(file => file.OldName.Contains(toRemove))
            .Select(file => new
            {
                file.OldPath,
                file.OldName,
                NewName = file.OldName.Replace(
                    toRemove,
                    toReplace
                ),
                file.Directory
            })
            .Where(file => file.OldName != file.NewName)
            .ToList();

        EditorGUILayout.LabelField(
            $"Files to rename: {renameOperations.Count}"
        );

        EditorGUILayout.Space(5);

        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition,
            GUILayout.MinHeight(100),
            GUILayout.MaxHeight(300)
        );

        foreach (var operation in renameOperations)
        {
            EditorGUILayout.LabelField(
                operation.OldName,
                EditorStyles.miniLabel
            );

            EditorGUILayout.LabelField(
                "-> " + operation.NewName,
                EditorStyles.boldLabel
            );

            EditorGUILayout.Space(3);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledScope(
                   renameOperations.Count == 0))
        {
            if (GUILayout.Button("Rename Files", GUILayout.Height(30)))
            {
                RenameFiles(renameOperations.Select(x =>
                    (x.OldPath, x.NewName)).ToArray());
            }
        }
    }

    private string GetFolderPath()
    {
        if (targetFolder == null)
            return null;

        string path = AssetDatabase.GetAssetPath(targetFolder);

        if (AssetDatabase.IsValidFolder(path))
            return path;

        return null;
    }

    private string[] GetFiles(string folderPath)
    {
        var searchOption = includeSubdirectories
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        return Directory.GetFiles(folderPath, "*", searchOption)
            .Where(path =>
                !path.EndsWith(".meta",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void RenameFiles((string oldPath, string newName)[] operations)
    {
        // Preflight: check all destinations before renaming anything.
        foreach (var operation in operations)
        {
            string directory = Path.GetDirectoryName(operation.oldPath);
            string newPath = Path.Combine(directory, operation.newName);

            if (File.Exists(newPath) || Directory.Exists(newPath))
            {
                // A destination may be another file that is also being
                // renamed, so check whether it is a source in this batch.
                bool isBatchSource = operations.Any(x =>
                    string.Equals(
                        x.oldPath,
                        newPath,
                        StringComparison.OrdinalIgnoreCase));

                if (!isBatchSource)
                {
                    EditorUtility.DisplayDialog(
                        "Rename Cancelled",
                        $"Destination already exists:\n{newPath}",
                        "OK"
                    );
                    return;
                }
            }
        }

        if (!EditorUtility.DisplayDialog(
                "Confirm Rename",
                $"Rename {operations.Length} file(s)?\n\n" +
                "This operation changes filenames on disk.",
                "Rename",
                "Cancel"))
        {
            return;
        }

        int renamed = 0;

        try
        {
            foreach (var operation in operations)
            {
                string directory =
                    Path.GetDirectoryName(operation.oldPath);

                string newPath = Path.Combine(
                    directory,
                    operation.newName
                );

                if (string.Equals(
                        operation.oldPath,
                        newPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (File.Exists(newPath) || Directory.Exists(newPath))
                {
                    throw new IOException(
                        $"Destination already exists: {newPath}"
                    );
                }

                File.Move(operation.oldPath, newPath);
                renamed++;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Batch rename stopped after {renamed} file(s).\n" +
                exception
            );

            EditorUtility.DisplayDialog(
                "Rename Partially Completed",
                $"{renamed} file(s) renamed before an error occurred.\n" +
                "Check the Console for details.",
                "OK"
            );

            AssetDatabase.Refresh();
            return;
        }

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Rename Complete",
            $"Successfully renamed {renamed} file(s).",
            "OK"
        );
    }
}
