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
        return step.goal switch
        {
            StepGoal.Talk => LastHas("talk", null),
            StepGoal.Grab => IsHolding(step.targetName) || LastHas("grab", step.targetName),
            StepGoal.PlaceOn => IsOnSurface(agentSystem.contextLibrary, step.targetName, step.surfaceName),
            StepGoal.MoveToSpot => IsNearSpot(agentSystem.contextLibrary, agentSystem.contextLibrary.agents[0], step.targetName),
            _ => false,
        };
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

    public bool IsNearSpot(ContextLibrary ctx, NPC agent, string spotName, float maxDistance = 0.1f)
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

    bool IsHolding(string itemName)
    {
        NPC npc = agentSystem.contextLibrary.agents[0];
        Transform item = npc.GetItem(true) ?? npc.GetItem(false);
        return item != null && item.name == itemName;
    }
}
