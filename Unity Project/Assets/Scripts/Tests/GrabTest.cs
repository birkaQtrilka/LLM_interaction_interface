using UnityEngine;

public class GrabTest : MonoBehaviour
{
    [SerializeField] NPC npc;
    [SerializeField] string targetName;
    [SerializeField] AnimationLibrary animationLibrary;
    [SerializeField] AgentSystem agentSystem;
    [SerializeField] bool testGrab = false;
    [SerializeField] bool testMove = false;

    void Update()
    {
        if(testGrab)
        {
            testGrab = false;
            animationLibrary.PlayAnimation(agentSystem, new ActionData
            {
                id = 1,
                name = "grab",
                parameters = new string[] { targetName },
                runAfter = new int[] {},
                delayBefore = 0f
            });
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
