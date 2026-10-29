using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MetaHumanAutoMerger : MonoBehaviour
{
    [Header("Assign from Scene (both must be at position 0,0,0)")]
    public Transform bodyRoot; // Drag AnneFleur_Body here
    public Transform headRoot; // Drag AnneFleur_Head here

    [ContextMenu("Merge MetaHuman Skeletons")]
    public void MergeMetaHuman()
    {
        if (bodyRoot == null || headRoot == null)
        {
            Debug.LogError("[MetaHumanAutoMerger] Please assign both bodyRoot and headRoot in the Inspector!");
            return;
        }

#if UNITY_EDITOR
        // 1. Unpack prefabs so Unity allows bone reparenting in the Editor
        if (PrefabUtility.IsPartOfAnyPrefab(bodyRoot))
            PrefabUtility.UnpackPrefabInstance(bodyRoot.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        if (PrefabUtility.IsPartOfAnyPrefab(headRoot))
            PrefabUtility.UnpackPrefabInstance(headRoot.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
#endif

        // 2. Index all existing Body bones by name
        Dictionary<string, Transform> bodyBones = new Dictionary<string, Transform>();
        foreach (Transform t in bodyRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!bodyBones.ContainsKey(t.name))
                bodyBones.Add(t.name, t);
        }

        // 3. Locate the duplicate 'root' bone inside the Head
        Transform headSkeletonRoot = null;
        foreach (Transform t in headRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "root" && t != headRoot)
            {
                headSkeletonRoot = t;
                break;
            }
        }

        if (headSkeletonRoot == null)
        {
            Debug.LogError("[MetaHumanAutoMerger] Could not find 'root' bone inside Head!");
            return;
        }

        // 4. Recursively transfer all unique facial & corrective branches to the Body skeleton
        TransferUniqueBones(headSkeletonRoot, bodyBones);

        // 5. Remap all SkinnedMeshRenderers across all LODs
        SkinnedMeshRenderer[] smrs = headRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int totalRemapped = 0;
        int missingCount = 0;

        foreach (SkinnedMeshRenderer smr in smrs)
        {
            Transform[] currentBones = smr.bones;
            Transform[] newBones = new Transform[currentBones.Length];

            for (int i = 0; i < currentBones.Length; i++)
            {
                if (currentBones[i] == null)
                {
                    missingCount++;
                    continue;
                }

                if (bodyBones.TryGetValue(currentBones[i].name, out Transform match))
                {
                    newBones[i] = match;
                    totalRemapped++;
                }
                else
                {
                    Debug.LogWarning($"[MetaHumanAutoMerger] Missing bone: '{currentBones[i].name}' on '{smr.name}'", smr);
                    missingCount++;
                }
            }

            smr.bones = newBones;

            // Remap root bone
            if (smr.rootBone != null && bodyBones.TryGetValue(smr.rootBone.name, out Transform newRoot))
            {
                smr.rootBone = newRoot;
            }

            // Ensure bounding boxes update correctly
            smr.updateWhenOffscreen = true;
        }

        // 6. Delete the empty duplicate head skeleton
        DestroyImmediate(headSkeletonRoot.gameObject);

        // 7. Remove any secondary Animator on the Head
        Animator headAnim = headRoot.GetComponent<Animator>();
        if (headAnim != null)
            DestroyImmediate(headAnim);

        // 8. Parent the Head object under the Body
        headRoot.SetParent(bodyRoot, true);

        if (missingCount == 0)
        {
            Debug.Log($"<color=green>[MetaHumanAutoMerger] Success!</color> Merged {totalRemapped} bone links across {smrs.Length} meshes with 0 missing bones.");
        }
        else
        {
            Debug.LogWarning($"[MetaHumanAutoMerger] Merged with {missingCount} unmapped bones. Check warnings above.");
        }
    }

    private void TransferUniqueBones(Transform currentHeadBone, Dictionary<string, Transform> bodyBones)
    {
        // Snapshot children so hierarchy changes don't disrupt iteration
        List<Transform> children = new List<Transform>();
        for (int i = 0; i < currentHeadBone.childCount; i++)
        {
            children.Add(currentHeadBone.GetChild(i));
        }

        foreach (Transform child in children)
        {
            if (bodyBones.TryGetValue(child.name, out Transform matchingBodyBone))
            {
                // This bone exists in both Body and Head (e.g. spine, neck) -> recurse down
                TransferUniqueBones(child, bodyBones);
            }
            else
            {
                // This bone is UNIQUE to the head (e.g. FACIAL_C_Neck1Root, FACIAL_C_FacialRoot, etc.)
                if (bodyBones.TryGetValue(currentHeadBone.name, out Transform targetParent))
                {
                    // Reparent to the corresponding bone in the body, keeping exact world transform
                    child.SetParent(targetParent, true);

                    // Register this bone and all its hundreds of children into bodyBones
                    foreach (Transform desc in child.GetComponentsInChildren<Transform>(true))
                    {
                        if (!bodyBones.ContainsKey(desc.name))
                            bodyBones.Add(desc.name, desc);
                    }
                }
                else
                {
                    Debug.LogError($"[MetaHumanAutoMerger] Cannot reparent '{child.name}': parent '{currentHeadBone.name}' not found in Body!");
                }
            }
        }
    }
}