using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ChildIsolator
{
    static ChildIsolator()
    {
        Selection.selectionChanged += OnSelectionChanged;
    }

    private static void OnSelectionChanged()
    {
        // Don't run in play mode
        if (Application.isPlaying) return;

        // Check if the selected object is a GameObject
        GameObject selectedGO = Selection.activeGameObject;
        if (selectedGO == null) return;

        // Check if the selected object has your target component (e.g. MyTest)
        MyTest testComponent = selectedGO.GetComponent<MyTest>();
        if (testComponent == null) return;

        Transform current = selectedGO.transform;
        Transform parent = current.parent;

        // Only isolate if it actually has siblings under a parent
        if (parent == null) return;

        // Record Undo so you can Ctrl+Z if needed
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        // Deactivate all siblings, activate current
        foreach (Transform sibling in parent)
        {
            if (sibling == current)
            {
                if (!sibling.gameObject.activeSelf)
                {
                    Undo.RecordObject(sibling.gameObject, "Activate Selected Child");
                    sibling.gameObject.SetActive(true);
                }
            }
            else
            {
                if (sibling.gameObject.activeSelf)
                {
                    Undo.RecordObject(sibling.gameObject, "Deactivate Sibling");
                    sibling.gameObject.SetActive(false);
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }
}
