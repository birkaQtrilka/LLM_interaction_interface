using UnityEngine;

public class GrabTest : MyTest
{
    [SerializeField] NPC npc;
    [SerializeField] string[] targetNames;
    [SerializeField] AnimationLibrary animationLibrary;
    [SerializeField] AgentSystem agentSystem;
    [SerializeField] bool testGrab = false;
    [SerializeField] bool testMove = false;

    void Update()
    {
        if(testGrab)
        {
            testGrab = false;
            if (animationLibrary.GetAnimation(x => x.data.id == 0) != null) return;
            int id = 0;
            string error = "";
            foreach (var targetName in targetNames)
            {
                error = animationLibrary.PlayAnimation(agentSystem, new ActionData
                {
                    id = id++,
                    agent = npc.name,
                    name = "moveTo",
                    parameters = new string[] { targetName },
                    runAfter = id == 1 ? new int[] {  } : new int[] { id - 2 },
                    delayBefore = 0f
                });
                if(error != null)
                {
                    Debug.LogError(error);
                    return;
                }
                animationLibrary.PlayAnimation(agentSystem, new ActionData
                {
                    id = id++,
                    agent = npc.name,
                    name = "grab",
                    parameters = new string[] { targetName },
                    runAfter = new int[] { id - 2 },
                    delayBefore = 0f
                });
                if (error != null)
                {
                    Debug.LogError(error);
                    return;
                }
                animationLibrary.PlayAnimation(agentSystem, new ActionData
                {
                    id = id++,
                    agent = npc.name,
                    name = "forceRelease",
                    parameters = new string[] {  },
                    runAfter = new int[] { id - 2 },
                    delayBefore = 0f
                });
                if (error != null)
                {
                    Debug.LogError(error);
                    return;
                }
            }
        }
        if(testMove)
        {
            testMove = false;
            animationLibrary.PlayAnimation(agentSystem, new ActionData
            {
                id = 1,
                name = "moveToPoint",
                parameters = new string[] { "1.51", "0.93", "-0.03" },
                runAfter = new int[] {},
                delayBefore = 0f
            });
        }
    }
    
}
