using System;
using UnityEngine;

public class GiveIKTest : MonoBehaviour
{
    [SerializeField] TestNames testName;
    [SerializeField] AnimationLibrary animationLibrary;
    [SerializeField] AgentSystem AgentSystem;
    public enum TestNames
    {
        Give,
    }

    void Start()
    {
        switch (testName)
        {
            case TestNames.Give:
                GiveTest();
                break;
        }
    }

    private void GiveTest()
    {
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 1,
            name = "moveTo",
            agent = "NPC1",
            parameters = new string[] { "phone" },
            runAfter = new int[] { },
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 2,
            name = "grab",
            agent = "NPC1",
            parameters = new string[] { "phone" },
            runAfter = new int[1] { 1 },
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 3,
            name = "moveTo",
            agent = "NPC1",
            parameters = new string[] { "NPC2" },
            runAfter = new int[] { 2 },
        });
        animationLibrary.PlayAnimation(AgentSystem, new ActionData
        {
            id = 4,
            name = "give",
            agent = "NPC1",
            parameters = new string[] { "NPC2" },
            runAfter = new int[1] { 3 },
        });
    }
}
