using UnityEngine;

public class AgentSameDestinationTest : MonoBehaviour
{
    public enum TestType
    {
        ReachDestination,
        TwoAgents
    }
    public TestType test;

    private void Start()
    {
        switch (test)
        {
            case TestType.ReachDestination:
                ReachDestination();
                break;
            case TestType.TwoAgents:
                TwoAgents();
                break;
        }
    }

    void ReachDestination()
    {
        var agents = FindObjectsByType<NPC>();
        var system = FindAnyObjectByType<AgentSystem>();
        var agent = agents[0];
        system.AnimationLibrary.PlayAnimation(system,
            new ActionData
            {
                id = 0,
                name = "moveTo",
                agent = agent.name,
                parameters = new string[] { "Chair" }
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
                    parameters = new string[] { "Chair" }
                }

            );
        }
    }
}
