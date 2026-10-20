using UnityEngine;

public class AgentSameDestinationTest : MonoBehaviour
{
    public enum TestType
    {
        ReachDestination,
        TwoAgents
    }
    public TestType test;
    [SerializeField] private string targetObjectName = "Chair";
    [SerializeField] private float startTime;
    private void Start()
    {
        switch (test)
        {
            case TestType.ReachDestination:
                Invoke(nameof(ReachDestination), startTime);
                break;
            case TestType.TwoAgents:
                Invoke(nameof(TwoAgents), startTime);
                break;
        }
    }

    void ReachDestination()
    {
        var agents = FindObjectsByType<NPC>();
        var system = FindAnyObjectByType<AgentSystem>();
        if(agents == null || agents.Length == 0)
        {
            Debug.LogWarning("No agents found");
            return;
        }
        var agent = agents[0];
        system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = 0,
                name = "moveTo",
                agent = agent.name,
                parameters = new string[] { targetObjectName }
            }
        );
    }

    void TwoAgents()
    {
        var agents = FindObjectsByType<NPC>();
        var system = FindAnyObjectByType<AgentSystem>();
        int id = 0;
        foreach (var agent in agents)
        {
            system.AnimationLibrary.PlayAnimation(system,
                new ActionData
                {
                    id = id++,
                    name = "moveTo",
                    agent = agent.name,
                    parameters = new string[] { targetObjectName }
                }

            );
        }
    }
}
