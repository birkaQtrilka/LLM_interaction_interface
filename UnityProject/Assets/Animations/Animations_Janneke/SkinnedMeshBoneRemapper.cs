#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Re-assigns the bones (and root bone) of SkinnedMeshRenderers to the matching bones
/// (matched by name) of an existing, already-merged skeleton.
///
/// Works on a prefab asset directly (no need to put it in a scene) or on a scene object.
/// Put this file in an "Editor" folder.
/// </summary>
public class SkinnedMeshBoneRemapper : EditorWindow
{
    [SerializeField] private GameObject targetRoot;          // merged character (prefab asset or scene object)
    [SerializeField] private GameObject boneReferenceModel;  // optional: the original Head FBX asset
    [SerializeField] private string nameFilter = "";
    [SerializeField] private bool skipAlreadyCorrect = true;
    [SerializeField] private bool updateWhenOffscreen = true;
    [SerializeField] private bool dryRun = false;

    private Vector2 scroll;

    [MenuItem("Tools/Stefan/Skinned Mesh Bone Remapper")]
    private static void ShowWindow()
    {
        GetWindow<SkinnedMeshBoneRemapper>("Bone Remapper");
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Skinned Mesh Bone Remapper", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Re-points the bones of every SkinnedMeshRenderer under 'Target' at the bones of the Target's own skeleton, matched by name.",
            MessageType.Info);

        targetRoot = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Target (merged character)", "The body prefab asset or a scene object that already contains the merged skeleton and the head renderers."),
            targetRoot, typeof(GameObject), true);

        boneReferenceModel = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Bone reference (Head FBX)", "Optional but recommended. The original Head FBX asset. Used to read the bone names for renderers whose bones are currently missing/null."),
            boneReferenceModel, typeof(GameObject), false);

        EditorGUILayout.Space();
        nameFilter = EditorGUILayout.TextField(
            new GUIContent("Renderer name filter", "Only process renderers whose name contains this text (case-insensitive). Leave empty for all."),
            nameFilter);
        skipAlreadyCorrect = EditorGUILayout.Toggle(
            new GUIContent("Skip already-correct", "Skip renderers whose bones are all assigned and already live under the Target."),
            skipAlreadyCorrect);
        updateWhenOffscreen = EditorGUILayout.Toggle("Update When Offscreen", updateWhenOffscreen);
        dryRun = EditorGUILayout.Toggle(
            new GUIContent("Dry run (log only)", "Reports what would change without modifying anything."),
            dryRun);

        EditorGUILayout.Space();

        if (targetRoot != null && IsModelAsset(targetRoot))
        {
            EditorGUILayout.HelpBox("The target is a model (FBX) asset, which is read-only. Use the prefab instead.", MessageType.Error);
        }

        if (boneReferenceModel != null && !EditorUtility.IsPersistent(boneReferenceModel))
        {
            EditorGUILayout.HelpBox("The bone reference should be an asset from the Project window, not a scene object.", MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(targetRoot == null || IsModelAsset(targetRoot)))
        {
            if (GUILayout.Button(dryRun ? "Run (Dry Run)" : "Remap Bones", GUILayout.Height(30)))
            {
                Run();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------

    private static bool IsModelAsset(GameObject go)
    {
        return PrefabUtility.IsPartOfPrefabAsset(go) &&
               PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Model;
    }

    private void Run()
    {
        if (PrefabUtility.IsPartOfPrefabAsset(targetRoot))
        {
            string path = AssetDatabase.GetAssetPath(targetRoot);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = Remap(contents, false);
                if (!dryRun && changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    Debug.Log($"[BoneRemapper] Saved prefab '{path}'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
        else
        {
            Undo.SetCurrentGroupName("Remap Skinned Mesh Bones");
            int group = Undo.GetCurrentGroup();
            Remap(targetRoot, true);
            Undo.CollapseUndoOperations(group);
        }
    }

    /// <returns>Number of renderers that were (or would be) changed.</returns>
    private int Remap(GameObject root, bool useUndo)
    {
        Dictionary<string, Transform> bonesByName = BuildBoneLookup(root.transform);

        SkinnedMeshRenderer[] referenceSmrs = boneReferenceModel != null
            ? boneReferenceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            : Array.Empty<SkinnedMeshRenderer>();

        int changed = 0, skipped = 0, failed = 0, linked = 0, missing = 0;

        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!string.IsNullOrEmpty(nameFilter) &&
                smr.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (smr.sharedMesh == null)
            {
                Debug.LogWarning($"[BoneRemapper] '{smr.name}' has no mesh. Skipped.", smr);
                failed++;
                continue;
            }

            if (skipAlreadyCorrect && IsAlreadyCorrect(smr, root.transform))
            {
                skipped++;
                continue;
            }

            if (!TryGetReferenceNames(smr, referenceSmrs, out string[] boneNames, out string rootBoneName))
            {
                Debug.LogError(
                    $"[BoneRemapper] '{smr.name}': its bones are missing and no matching renderer was found in the bone reference model. " +
                    "Assign the Head FBX as 'Bone reference'.", smr);
                failed++;
                continue;
            }

            if (boneNames.Length != smr.sharedMesh.bindposeCount)
            {
                Debug.LogError(
                    $"[BoneRemapper] '{smr.name}': reference has {boneNames.Length} bones but the mesh has " +
                    $"{smr.sharedMesh.bindposeCount} bindposes. Wrong reference model? Skipped.", smr);
                failed++;
                continue;
            }

            Transform[] newBones = new Transform[boneNames.Length];
            int localMissing = 0;

            for (int i = 0; i < boneNames.Length; i++)
            {
                if (string.IsNullOrEmpty(boneNames[i]))
                {
                    localMissing++;
                    continue;
                }

                if (bonesByName.TryGetValue(boneNames[i], out Transform match))
                {
                    newBones[i] = match;
                    linked++;
                }
                else
                {
                    Debug.LogWarning($"[BoneRemapper] Missing bone '{boneNames[i]}' on '{smr.name}'", smr);
                    localMissing++;
                }
            }

            missing += localMissing;
            changed++;

            if (dryRun)
            {
                continue;
            }

            if (useUndo)
            {
                Undo.RecordObject(smr, "Remap Skinned Mesh Bones");
            }

            smr.bones = newBones;

            if (!string.IsNullOrEmpty(rootBoneName) && bonesByName.TryGetValue(rootBoneName, out Transform newRoot))
            {
                smr.rootBone = newRoot;
            }

            if (updateWhenOffscreen)
            {
                smr.updateWhenOffscreen = true;
            }

            EditorUtility.SetDirty(smr);

            if (useUndo)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(smr))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
                }

                if (smr.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(smr.gameObject.scene);
                }
            }
        }

        string prefix = dryRun ? "[BoneRemapper] (dry run) " : "[BoneRemapper] ";
        string summary =
            $"{prefix}{changed} renderer(s) remapped, {skipped} skipped (already correct), {failed} failed. " +
            $"{linked} bone links made, {missing} unmapped.";

        if (failed > 0 || missing > 0) Debug.LogWarning(summary);
        else Debug.Log(summary);

        return changed;
    }

    private static Dictionary<string, Transform> BuildBoneLookup(Transform root)
    {
        var dict = new Dictionary<string, Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!dict.ContainsKey(t.name))
            {
                dict.Add(t.name, t);
            }
        }
        return dict;
    }

    private static bool IsAlreadyCorrect(SkinnedMeshRenderer smr, Transform root)
    {
        Transform[] bones = smr.bones;
        if (bones == null || bones.Length == 0) return false;

        foreach (Transform b in bones)
        {
            if (b == null || !b.IsChildOf(root)) return false;
        }

        return smr.rootBone != null && smr.rootBone.IsChildOf(root);
    }

    /// <summary>
    /// Finds the bone names this renderer should use. Prefers the reference model (matched by shared mesh,
    /// then by renderer name); otherwise falls back to the renderer's own current bones.
    /// </summary>
    private static bool TryGetReferenceNames(
        SkinnedMeshRenderer smr,
        SkinnedMeshRenderer[] referenceSmrs,
        out string[] boneNames,
        out string rootBoneName)
    {
        SkinnedMeshRenderer reference = null;

        foreach (SkinnedMeshRenderer r in referenceSmrs)
        {
            if (r.sharedMesh != null && r.sharedMesh == smr.sharedMesh)
            {
                reference = r;
                break;
            }
        }

        if (reference == null)
        {
            foreach (SkinnedMeshRenderer r in referenceSmrs)
            {
                if (r.name == smr.name)
                {
                    reference = r;
                    break;
                }
            }
        }

        SkinnedMeshRenderer source = reference != null ? reference : smr;
        Transform[] bones = source.bones;

        boneNames = new string[bones.Length];
        bool anyBone = false;

        for (int i = 0; i < bones.Length; i++)
        {
            boneNames[i] = bones[i] != null ? bones[i].name : null;
            anyBone |= bones[i] != null;
        }

        rootBoneName = source.rootBone != null ? source.rootBone.name : null;
        return anyBone;
    }
}
#endif