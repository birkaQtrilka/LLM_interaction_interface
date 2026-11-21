using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AgentRig : IDisposable
{
    /// <summary>Prefab with NPC, NavMeshAgent, EmotionalStateHandler and HandController. Change if yours lives elsewhere.</summary>
    public static string NpcPrefabPath = "Assets/Resources/Prefabs/NPC.prefab";

    public GameObject Root;
    public AnimationLibrary Anim;
    public ContextLibrary Ctx;
    public AgentSystem System;
    public MockLLMBackend Backend = new();
    public NPC Nurse;
    NavMeshDataInstance navFloor;

    public static AgentRig Create()
    {
        var rig = new AgentRig();
        rig.navFloor = AddNavMeshFloor();                        // before the NPC exists, so its agent can snap to it

        rig.Root = new GameObject("AgentRig");
        rig.Anim = rig.Root.AddComponent<AnimationLibrary>();    // Awake builds the action registry
        rig.Ctx = rig.Root.AddComponent<ContextLibrary>();       // Awake scans the scene, so create the NPC AFTER this
        rig.System = rig.Root.AddComponent<AgentSystem>();
        rig.System.Init(rig.Backend, rig.Anim, rig.Ctx);
        rig.System.Configure(maxFeedbackRounds: 2, errorBatchDelay: 0f);
        rig.System.sendAllContext = true;                        // skip round 1 unless a test wants it

        rig.Nurse = CreateNpc("Nurse");
        rig.Ctx.agents.Add(rig.Nurse);
        return rig;
    }

    static NPC CreateNpc(string name)
    {
        GameObject prefab = null;
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
#endif
        if (prefab == null)
            throw new InvalidOperationException($"AgentRig: NPC prefab not found at '{NpcPrefabPath}'. Set AgentRig.NpcPrefabPath.");

        var go = UnityEngine.Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        go.name = name;
        return go.GetComponent<NPC>();
    }

    // 20x20 walkable floor with its top at y = 0, using only core UnityEngine.AI (no package needed)
    static NavMeshDataInstance AddNavMeshFloor()
    {
        var source = new NavMeshBuildSource
        {
            shape = NavMeshBuildSourceShape.Box,
            size = new Vector3(20f, 0.1f, 20f),
            transform = Matrix4x4.TRS(new Vector3(0f, -0.05f, 0f), Quaternion.identity, Vector3.one),
            area = 0
        };
        var data = NavMeshBuilder.BuildNavMeshData(
            NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource> { source },
            new Bounds(Vector3.zero, new Vector3(20f, 2f, 20f)), Vector3.zero, Quaternion.identity);
        return NavMesh.AddNavMeshData(data);
    }

    public void Dispose()
    {
        if (Nurse != null) UnityEngine.Object.DestroyImmediate(Nurse.gameObject);
        if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
        if (navFloor.valid) navFloor.Remove();
    }
}

public static class T
{
    public static ActionData Action(int id, string name, string agent = "Nurse",
        string[] parameters = null, int[] runAfter = null, float delay = 0f)
        => new ActionData
        {
            id = id,
            name = name,
            agent = agent,
            parameters = parameters ?? new string[0],
            runAfter = runAfter ?? new int[0],
            delayBefore = delay
        };

    public static ActionsResponse Plan(params ActionData[] actions) => new ActionsResponse { actions = actions };

    public static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

    public static IEnumerator Until(Func<bool> cond, string what, float timeout = 3f)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!cond())
        {
            if (Time.realtimeSinceStartup > end) NUnit.Framework.Assert.Fail($"Timed out waiting for: {what}");
            yield return null;
        }
    }
}