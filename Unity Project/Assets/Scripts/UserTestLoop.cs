using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UserTestLoop : MonoBehaviour
{
    public enum StepGoal
    {
        Talk,
        Grab,
        PlaceOn,
        MoveToSpot,
        // The item must leave the hand that held it when the message was sent
        HandOff,
        // Some agent must be on a marked spot they were not on when the message was sent
        ArriveAtSpot,
    }

    [Serializable]
    public class Step
    {
        public string instruction;
        public StepGoal goal;
        public string targetName;
        public string surfaceName;
    }

    [SerializeField] AgentSystem agentSystem;
    [SerializeField] PanelRenderer panelRenderer;
    [SerializeField] LLMBackend llmBackend;
    [SerializeField] Step[] steps;

    int current;
    bool waiting;
    // Who held the handoff item when this message was sent
    NPC holderAtSend;
    // Spot each agent occupied when this message was sent. Null means none
    readonly List<(NPC agent, string spot)> placesAtSend = new();
    Label stepLabel;
    Label instructionLabel;
    Label statusLabel;
    Label formStatus;
    VisualElement hud;
    VisualElement complete;
    readonly Button[] naturalChoices = new Button[5];
    readonly Button[] accurateChoices = new Button[5];
    readonly Action[] naturalClicks = new Action[5];
    readonly Action[] accurateClicks = new Action[5];
    int naturalValue = -1;
    int accurateValue = -1;
    TextField naturalNote;
    TextField accurateNote;
    Button confirmButton;

    void OnEnable()
    {
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
        if (agentSystem.chatManager != null)
        {
            agentSystem.chatManager.OnTextSent.AddListener(OnUserMessage);
        }
    }

    void OnDisable()
    {
        if (panelRenderer != null)
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }
        if (agentSystem != null && agentSystem.chatManager != null)
        {
            agentSystem.chatManager.OnTextSent.RemoveListener(OnUserMessage);
        }
        if (confirmButton != null)
        {
            confirmButton.clicked -= OnConfirm;
        }
        UnbindScores();
    }

    [ContextMenu("Show end form")]
    void ShowEndForm()
    {
        if (steps == null) return;
        current = steps.Length;
        waiting = false;
        ShowCurrent();
    }

    void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        if (confirmButton != null)
        {
            confirmButton.clicked -= OnConfirm;
        }
        UnbindScores();

        hud = root.Q("hud");
        complete = root.Q("complete");
        stepLabel = root.Q<Label>("step-label");
        instructionLabel = root.Q<Label>("instruction");
        statusLabel = root.Q<Label>("status");
        formStatus = root.Q<Label>("form-status");
        naturalNote = root.Q<TextField>("natural-note");
        accurateNote = root.Q<TextField>("accurate-note");
        confirmButton = root.Q<Button>("confirm");
        naturalValue = -1;
        accurateValue = -1;
        BindScores(root, "natural-score", naturalChoices, naturalClicks, PickNatural);
        BindScores(root, "accurate-score", accurateChoices, accurateClicks, PickAccurate);
        if (confirmButton != null) confirmButton.clicked += OnConfirm;
        ShowCurrent();
    }

    void BindScores(VisualElement root, string groupName, Button[] choices, Action[] clicks, Action<int> pick)
    {
        for (int i = 0; i < choices.Length; i++)
        {
            int score = i + 1;
            Button button = root.Q<Button>(groupName + "-" + score);
            choices[i] = button;
            if (button == null) continue;
            button.RemoveFromClassList("selected");
            clicks[i] = () => pick(score);
            button.clicked += clicks[i];
        }
    }

    void UnbindScores()
    {
        UnbindRow(naturalChoices, naturalClicks);
        UnbindRow(accurateChoices, accurateClicks);
    }

    static void UnbindRow(Button[] choices, Action[] clicks)
    {
        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i] != null && clicks[i] != null)
                choices[i].clicked -= clicks[i];
        }
    }

    void PickNatural(int score) => Pick(naturalChoices, ref naturalValue, score);

    void PickAccurate(int score) => Pick(accurateChoices, ref accurateValue, score);

    static void Pick(Button[] choices, ref int value, int score)
    {
        value = score;
        for (int i = 0; i < choices.Length; i++)
        {
            if (choices[i] == null) continue;
            if (i == score - 1) choices[i].AddToClassList("selected");
            else choices[i].RemoveFromClassList("selected");
        }
    }

    static void SetRowEnabled(Button[] choices, bool enabled)
    {
        foreach (Button choice in choices)
        {
            if (choice != null) choice.SetEnabled(enabled);
        }
    }

    void OnConfirm()
    {
        if (llmBackend == null)
        {
            if (formStatus != null) formStatus.text = "The form is not set up";
            return;
        }
        if (naturalValue < 1 || accurateValue < 1)
        {
            formStatus.text = "Choose a score from 1 to 5 for both questions";
            return;
        }

        confirmButton.SetEnabled(false);
        formStatus.text = "Saving...";
        string error = llmBackend.SubmitFeedback(
            naturalValue,
            naturalNote != null ? naturalNote.value : "",
            accurateValue,
            accurateNote != null ? accurateNote.value : ""
        );
        if (error != null)
        {
            Debug.LogError(error);
            confirmButton.SetEnabled(true);
            formStatus.text = "Could not save. Try again";
            return;
        }

        SetRowEnabled(naturalChoices, false);
        SetRowEnabled(accurateChoices, false);
        if (naturalNote != null) naturalNote.SetEnabled(false);
        if (accurateNote != null) accurateNote.SetEnabled(false);
        formStatus.text = "Saved";
    }

    void OnUserMessage(string message)
    {
        if (waiting || current >= steps.Length) return;
        StartCoroutine(AfterTurn());
    }

    IEnumerator AfterTurn()
    {
        waiting = true;
        Step step = steps[current];
        if (step.goal == StepGoal.HandOff)
            holderAtSend = FindHolder(step.targetName);
        if (step.goal == StepGoal.ArriveAtSpot)
            SnapshotPlaces();
        statusLabel.text = "Waiting...";

        yield return null;
        yield return new WaitUntil(() => !agentSystem.IsBusy);
        yield return null;
        yield return new WaitUntil(() => agentSystem.AnimationLibrary.animations.Count == 0);

        if (StepPassed(steps[current]))
        {
            current++;
            ShowCurrent();
        }
        else
        {
            statusLabel.text = "That did not finish this step. Try again";
        }

        waiting = false;
    }

    void ShowCurrent()
    {
        if (hud == null || complete == null) return;

        if (current >= steps.Length)
        {
            hud.AddToClassList("hidden");
            complete.RemoveFromClassList("hidden");
            return;
        }

        complete.AddToClassList("hidden");
        hud.RemoveFromClassList("hidden");
        stepLabel.text = $"{current + 1} / {steps.Length}";
        instructionLabel.text = steps[current].instruction;
        statusLabel.text = "";
    }

    bool StepPassed(Step step)
    {
        return step.goal switch
        {
            StepGoal.Talk => LastHas("talk", null),
            StepGoal.Grab => FindHolder(step.targetName) != null,
            StepGoal.PlaceOn => IsOnSurface(agentSystem.contextLibrary, step.targetName, step.surfaceName),
            StepGoal.MoveToSpot => SomeAgentNear(step.targetName),
            StepGoal.HandOff => HandedOff(step.targetName),
            StepGoal.ArriveAtSpot => SomeAgentChangedSpot(),
            _ => false,
        };
    }

    bool HandedOff(string itemName)
    {
        NPC now = FindHolder(itemName);
        return holderAtSend != null && now != null && now != holderAtSend;
    }

    public bool IsOnSurface(ContextLibrary ctx, string itemName, string surfaceName)
    {
        ContextItem surface = ctx.environment.Find(x => x.GetName() == surfaceName);
        if (surface == null) return false;

        surface.RecalculateBounds();
        surface.FindNeighbors();
        Transform item = null;
        if (surface.neighbors != null)
        {
            foreach (Transform neighbor in surface.neighbors)
            {
                if (neighbor != null && neighbor.name == itemName)
                {
                    item = neighbor;
                    break;
                }
            }
        }
        if (item == null) return false;

        Bounds box = surface.boundingBox;
        Vector3 p = item.position;
        return p.y >= box.max.y
            && p.x >= box.min.x && p.x <= box.max.x
            && p.z >= box.min.z && p.z <= box.max.z;
    }

    // A finished moveTo reserves a ring at the body radius plus half a metre, then may sample that far again
    const float SpotReach = 1.6f;

    public bool IsNearSpot(ContextLibrary ctx, NPC agent, string spotName, float maxDistance = SpotReach)
    {
        ContextItem spot = ctx.spots.Find(x => x.GetName() == spotName);
        if (spot == null || agent == null) return false;

        Vector3 npcPos = agent.transform.position;
        npcPos.y = 0;
        Vector3 spotPos = spot.transform.position;
        spotPos.y = 0;
        return Vector3.Distance(npcPos, spotPos) < maxDistance;
    }

    bool LastHas(string actionName, string target)
    {
        var last = agentSystem.LastActions;
        if (last?.actions == null) return false;

        foreach (var action in last.actions)
        {
            if (action.name != actionName) continue;
            if (string.IsNullOrEmpty(target)) return true;
            if (action.parameters != null && action.parameters.Length > 0 && action.parameters[0] == target)
            {
                return true;
            }
        }

        return false;
    }

    NPC FindHolder(string itemName)
    {
        foreach (NPC npc in agentSystem.contextLibrary.agents)
        {
            if (IsHolding(itemName, npc)) return npc;
        }
        return null;
    }

    bool IsHolding(string itemName, NPC npc)
    {
        if (npc == null) return false;
        Transform right = npc.GetItem(true);
        return NameIs(right, itemName);
    }

    static bool NameIs(Transform item, string itemName)
    {
        return item != null && item.name == itemName;
    }

    bool SomeAgentNear(string spotName)
    {
        foreach (NPC agent in agentSystem.contextLibrary.agents)
        {
            if (string.IsNullOrEmpty(spotName))
            {
                if (SpotOf(agent) != null) return true;
            }
            else if (IsNearSpot(agentSystem.contextLibrary, agent, spotName))
            {
                return true;
            }
        }
        return false;
    }

    void SnapshotPlaces()
    {
        placesAtSend.Clear();
        foreach (NPC agent in agentSystem.contextLibrary.agents)
            placesAtSend.Add((agent, SpotOf(agent)));
    }

    bool SomeAgentChangedSpot()
    {
        foreach (NPC agent in agentSystem.contextLibrary.agents)
        {
            string now = SpotOf(agent);
            if (now == null) continue;
            string before = null;
            foreach ((NPC who, string spot) in placesAtSend)
            {
                if (who == agent) before = spot;
            }
            if (now != before) return true;
        }
        return false;
    }

    string SpotOf(NPC agent)
    {
        ContextLibrary ctx = agentSystem.contextLibrary;
        foreach (ContextItem spot in ctx.spots)
        {
            if (IsNearSpot(ctx, agent, spot.GetName())) return spot.GetName();
        }
        return null;
    }
}
