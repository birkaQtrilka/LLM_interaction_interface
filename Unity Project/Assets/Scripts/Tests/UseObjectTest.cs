using UnityEngine;

public class UseObjectTest : MyTest
{
    [TextArea]
    [SerializeField] string script = "Laptop";
    private void Start()
    {
        NoParameterGesture(script);
    }

    void NoParameterGesture(string itemName)
    {
        var agents = FindObjectsByType<NPC>();
        var system = FindAnyObjectByType<AgentSystem>();
        if (agents == null || agents.Length == 0)
        {
            Debug.LogWarning("No agents found");
            return;
        }
        if(system.GetObject(itemName) == null)
        {
            Debug.LogWarning($"No object with name '{itemName}' found in the context library");
            return;
        }
        if (system.GetAnim(0) != null) return;

        var agent = agents[0];
        int id = 0;
        string resl;
        res = system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = id++,
                name = "moveTo",
                agent = agent.name,
                parameters = new string[] { itemName }
                
            }
        );
        if(res != null) { Debug.LogError(res); return; }
        res = system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = id++,
                name = "grab",
                agent = agent.name,
                runAfter = new int[] { id -2},
                parameters = new string[] { itemName }
            }
        );
        if (res != null) { Debug.LogError(res); return; }

        res = system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = id++,
                name = "moveTo",
                agent = agent.name,
                runAfter = new int[] { id -2},
                parameters = new string[] { agents[1].name }
            }
        );
        if (res != null) { Debug.LogError(res); return; }

        res = system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = id++,
                name = "use",
                agent = agent.name,
                runAfter = new int[] { id -2},
                parameters = new string[] { agents[1].name }
            }
        );
        if (res != null) { Debug.LogError(res); return; }

        Debug.Log(system.AnimationLibrary.ShowAnimationActions());
    }
}
