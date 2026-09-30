using UnityEngine;

public class AnimationQueueTest : MonoBehaviour
{
    [SerializeField] TestNames testName;
    [SerializeField] AnimationLibrary animationLibrary;
    [SerializeField] AgentSystem AgentSystem;
    public enum TestNames
    {
        OrderTest,
        GoGrabPhone,
        GoGrabAndPlacePhone,
    }

    void Start()
    {
        switch (testName)
        {
            case TestNames.OrderTest:
                OrderTest();
                break;
            case TestNames.GoGrabPhone:
                GrabPhone();
                break;
            case TestNames.GoGrabAndPlacePhone:
                GrabAndPlacePhone();
                break;
        }
    }

    public void GrabPhone()
    {
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 1,
            name = "moveTo",
            agent = "NPC",
            parameters = new string[] { "phone" },
            runAfter = new int[] { },
            delayBefore = 0
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 2,
            name = "grab",
            agent = "NPC",
            parameters = new string[] { "phone" },
            runAfter = new int[1] { 1 },
            delayBefore = 0
        });
    }
    
    public void GrabAndPlacePhone()
    {
        // used to activate boundary update, so phone can be placed
        AgentSystem.contextLibrary.GetContext(ContextQuery.GetFullContext(), animationLibrary.animations);

        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 1,
            name = "moveTo",
            agent = "NPC",
            parameters = new string[] { "phone" },
            runAfter = new int[] { },
            delayBefore = 0
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 2,
            name = "grab",
            agent = "NPC",
            parameters = new string[] { "phone" },
            runAfter = new int[1] { 1 },
            delayBefore = 0
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 3,
            name = "place",
            agent = "NPC",
            parameters = new string[] { "Chair" },
            runAfter = new int[1] { 2 },
            delayBefore = 0
        });
    }

    public void OrderTest()
    {
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 1,
            name = "moveTo",
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
            runAfter = new int[] { },
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
