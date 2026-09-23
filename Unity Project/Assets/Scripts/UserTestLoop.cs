using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class UserTestLoop : MonoBehaviour
{
    public enum StepGoal
    {
        Talk,
        Grab,
        PlaceOn,
        MoveToSpot
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
    [SerializeField] Step[] steps;

    int current;
    bool waiting;
    Label stepLabel;
    Label instructionLabel;
    Label statusLabel;
    VisualElement hud;
    VisualElement complete;

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
    }

    void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        hud = root.Q("hud");
        complete = root.Q("complete");
        stepLabel = root.Q<Label>("step-label");
        instructionLabel = root.Q<Label>("instruction");
        statusLabel = root.Q<Label>("status");
        ShowCurrent();
    }

    void OnUserMessage(string message)
    {
        if (waiting || current >= steps.Length) return;
        StartCoroutine(AfterTurn());
    }

    IEnumerator AfterTurn()
    {
        waiting = true;
        statusLabel.text = "Waiting for the nurse...";

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
        switch (step.goal)
        {
            case StepGoal.Talk:
                return LastHas("talk", null);
            case StepGoal.Grab:
                return IsHolding(step.targetName) || LastHas("grab", step.targetName);
            case StepGoal.PlaceOn:
                return agentSystem.contextLibrary.IsOnSurface(step.targetName, step.surfaceName);
            case StepGoal.MoveToSpot:
                return agentSystem.contextLibrary.IsNearSpot(step.targetName);
            default:
                return false;
        }
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

    bool IsHolding(string itemName)
    {
        NPC npc = agentSystem.contextLibrary.agent;
        Transform item = npc.GetItem(true) ?? npc.GetItem(false);
        return item != null && item.name == itemName;
    }
}
