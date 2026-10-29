using UnityEngine;

public class GestureTest : MyTest
{
    [TextArea]
    [SerializeField] string script = "I want to do a nod[nod] gesture NOW";
    private void OnEnable()
    {
        NoParameterGesture();
    }

    void NoParameterGesture()
    {
        var agents = FindObjectsByType<NPC>();
        var system = FindAnyObjectByType<AgentSystem>();
        if (agents == null || agents.Length == 0)
        {
            Debug.LogWarning("No agents found");
            return;
        }
        if (system.GetAnim(0) != null) return;

        var agent = agents[0];
        system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = 0,
                name = "talk",
                agent = agent.name,
                parameters = new string[] { script, "user" }
            }
        );
    }
}
