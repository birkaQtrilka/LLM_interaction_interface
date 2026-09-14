using UnityEngine;

public class AnimationQueueTest : MonoBehaviour
{
    [SerializeField] AnimationLibrary animationLibrary;
    [SerializeField] AgentSystem AgentSystem;

    void Start()
    {
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 1,
            name = "moveToSpot",
            parameters = new string[] { "SpotA" },
            runAfter = new int[] { },
            delayBefore = 0
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 2,
            name = "moveToSpot",
            parameters = new string[] { "SpotB" },
            runAfter = new int[] { 1 },
            delayBefore = 0
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 3,
            name = "talk",
            parameters = new string[] { "I should be talking While going to Spot B" },
            runAfter = new int[] { 1 },
            delayBefore = 1
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 4,
            name = "talk",
            parameters = new string[] { "I should be talking While going to Spot A" },
            runAfter = new int[] {  },
            delayBefore = 0
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 5,
            name = "talk",
            parameters = new string[] { "I should be talking AGAIN While going to Spot A" },
            runAfter = new int[] { },
            delayBefore = 1
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 6,
            name = "count",
            parameters = new string[] { "10" },
            runAfter = new int[] { 1 },
            delayBefore = 0
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 7,
            name = "talk",
            parameters = new string[] { "Talking after 2 dependencies" },
            runAfter = new int[] { 6, 2 },
            delayBefore = 0
        });
    }

}
