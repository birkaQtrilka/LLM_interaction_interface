using System;
using System.Collections.Generic;
using UnityEngine;

// When the player has been idle, an NPC says a designer-written line for the current checkpoint. No model call
// Add this component only to scenes that play the scenario
public class IdleNudge : MonoBehaviour
{
    [Serializable]
    public class CheckpointLines
    {
        // id from Backend/scenario/checkpoints.json
        public int checkpoint;
        // Exact name of the NPC that says the line
        public string speaker;
        // One is picked at random. Gesture tags work as in a talk action
        [TextArea] public string[] lines;
    }

    // Model action ids count up from 0, so a negative id never clashes with one still in the list
    const int NudgeActionId = -1;

    [SerializeField] AgentSystem system;
    // Seconds without a player message, while nothing runs, before the nudge
    [SerializeField] float idleSeconds = 30f;
    // A checkpoint without an entry gets no nudge, for example the last one
    [SerializeField] List<CheckpointLines> nudges = new();

    float idleTime;
    // Once per idle stretch: set by a nudge, cleared when the player sends a message
    bool nudged;

    void Start()
    {
        if (system == null || system.chatManager == null)
        {
            Debug.LogError("IdleNudge needs an AgentSystem with a ChatManager, so no nudges will play");
            enabled = false;
            return;
        }
        system.chatManager.OnTextSent.AddListener(OnPlayerMessage);
    }

    void OnDestroy()
    {
        if (system != null && system.chatManager != null)
            system.chatManager.OnTextSent.RemoveListener(OnPlayerMessage);
    }

    void OnPlayerMessage(string message)
    {
        idleTime = 0f;
        nudged = false;
    }

    void Update()
    {
        // Waiting for NPCs to finish acting is not idling
        if (!system.IsSettled)
        {
            idleTime = 0f;
            return;
        }
        idleTime += Time.deltaTime;
        if (nudged || idleTime < idleSeconds) return;

        nudged = true;
        Nudge();
    }

    void Nudge()
    {
        CheckpointLines entry = nudges.Find(n => n.checkpoint == system.CurrentCheckpoint);
        if (entry == null || entry.lines == null || entry.lines.Length == 0) return;

        string line = entry.lines[UnityEngine.Random.Range(0, entry.lines.Length)];
        // Played as a normal talk, so it shows in the chat and lands in the history the model and the judge read
        var talk = new ActionData
        {
            id = NudgeActionId,
            name = "talk",
            agent = entry.speaker,
            parameters = new[] { line, "user", "false" },
            runAfter = new int[0],
        };
        string error = system.AnimationLibrary.PlayAnimation(system, talk);
        if (error != null)
        {
            Debug.LogError($"Idle nudge failed: {error}");
            system.chatManager.AddChat("System: Idle nudge failed: " + error);
        }
    }
}
