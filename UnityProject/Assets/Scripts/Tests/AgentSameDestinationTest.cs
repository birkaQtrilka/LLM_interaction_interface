using UnityEngine;

public class AgentSameDestinationTest : MyTest
{
    public enum TestType
    {
        ReachDestination,
        TwoAgents, 
        ReachAndUseDestinations,
    }
    public TestType test;
    [SerializeField] private string targetObjectName = "Chair";
    [SerializeField] private float startTime;
    [SerializeField] private string[] useDestinations;
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
            case TestType.ReachAndUseDestinations:
                ReachAndUseDestinations();
                break;
        }
    }

    void ReachAndUseDestinations()
    {
        var agents = FindObjectsByType<NPC>();
        var system = FindAnyObjectByType<AgentSystem>();
        if(agents == null || agents.Length == 0)
        {
            Debug.LogWarning("No agents found");
            return;
        }
        var agent = agents[0];
        int id = 0;
        foreach (var useItem in useDestinations)
        {
            system.AnimationLibrary.PlayAnimation(system,
                new ActionData
                {
                    id = id++,
                    name = "moveTo",
                    agent = agent.name,
                    runAfter = id == 1 ? null : new int[] { id-2 },
                    parameters = new string[] { system.GetObject(useItem).GetName() }
                }
            );
            var obj = system.GetObject(useItem);
            if(obj.transform.TryGetComponent<GrabbableItem>(out var grabbable))
            {
                system.AnimationLibrary.PlayAnimation(system,
                new ActionData
                {
                    id = id++,
                    name = "grab",
                    agent = agent.name,
                    runAfter = new int[] { id - 2 },
                    parameters = new string[] { obj.GetName() }
                }
                );
                continue;
            }
            system.AnimationLibrary.PlayAnimation(system,
                new ActionData
                {
                    id = id++,
                    name = "use",
                    agent = agent.name,
                    runAfter = new int[] { id - 2 },
                    parameters = new string[] { obj.GetName() }
                }
            );

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
