#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Copies the materials from one list of SkinnedMeshRenderers to another, matched by index.
/// Place this file in an "Editor" folder (or leave the #if guard as is).
/// </summary>
public class MaterialTransfer : EditorWindow
{
    [SerializeField] private List<SkinnedMeshRenderer> fromRenderers = new List<SkinnedMeshRenderer>();
    [SerializeField] private List<SkinnedMeshRenderer> toRenderers = new List<SkinnedMeshRenderer>();

    private SerializedObject serializedWindow;
    private SerializedProperty fromProp;
    private SerializedProperty toProp;
    private Vector2 scroll;

    [MenuItem("Tools/Stefan/MaterialTransfer")]
    private static void ShowWindow()
    {
        GetWindow<MaterialTransfer>("Material Transfer");
    }

    private void OnEnable()
    {
        serializedWindow = new SerializedObject(this);
        fromProp = serializedWindow.FindProperty(nameof(fromRenderers));
        toProp = serializedWindow.FindProperty(nameof(toRenderers));
    }

    private void OnGUI()
    {
        serializedWindow.Update();

        EditorGUILayout.LabelField("Material Transfer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Materials are copied from 'From' to 'To', matched by list index.",
            MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.PropertyField(fromProp, true);
        EditorGUILayout.PropertyField(toProp, true);
        EditorGUILayout.EndScrollView();

        serializedWindow.ApplyModifiedProperties();

        bool lengthsMatch = fromRenderers.Count == toRenderers.Count;
        bool hasEntries = fromRenderers.Count > 0;

        if (!lengthsMatch)
        {
            EditorGUILayout.HelpBox(
                $"'From' ({fromRenderers.Count}) and 'To' ({toRenderers.Count}) must have the same length.",
                MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Swap From / To"))
            {
                (fromRenderers, toRenderers) = (toRenderers, fromRenderers);
                OnEnable(); // re-bind serialized properties to the swapped lists
            }

            if (GUILayout.Button("Clear"))
            {
                Undo.RecordObject(this, "Clear Material Transfer Lists");
                fromRenderers.Clear();
                toRenderers.Clear();
            }
        }

        using (new EditorGUI.DisabledScope(!lengthsMatch || !hasEntries))
        {
            if (GUILayout.Button("Transfer Materials", GUILayout.Height(30)))
            {
                Transfer();
            }
        }
    }

    private void Transfer()
    {
        int transferred = 0;

        Undo.SetCurrentGroupName("Transfer Materials");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < fromRenderers.Count; i++)
        {
            SkinnedMeshRenderer from = fromRenderers[i];
            SkinnedMeshRenderer to = toRenderers[i];

            if (from == null || to == null)
            {
                Debug.LogWarning($"[MaterialTransfer] Skipping index {i}: missing renderer.", this);
                continue;
            }

            if (from == to)
            {
                continue;
            }

            Material[] source = from.sharedMaterials; // returns a copy of the array

            if (source.Length != to.sharedMaterials.Length)
            {
                Debug.LogWarning(
                    $"[MaterialTransfer] '{from.name}' has {source.Length} material slot(s) but " +
                    $"'{to.name}' has {to.sharedMaterials.Length}. Copying anyway.", to);
            }

            Undo.RecordObject(to, "Transfer Materials");
            to.sharedMaterials = source; // sharedMaterials avoids creating leaked material instances in edit mode
            EditorUtility.SetDirty(to);
            transferred++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"[MaterialTransfer] Transferred materials on {transferred} renderer(s).");
    }
}
#endif