using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

public class Combo_System_Tests
{
    ComboSystem system;
    public string testText = "Hi there![wave] I'm happy to see so [all] many people gathering to support a [nod] good cause. Special thanks [point target=NPC1] to John for organizing the food booth";

    [SetUp]
    public void SetupScene()
    {
        GameObject go = new GameObject("ComboSystem");
        system = go.AddComponent<ComboSystem>();
    }

    [Test]
    public void Test_ProcessText_Parses_Commands()
    {
        string testText = "Hello[wave] world[nod]!";
        ParsedLine line = LineParser.Parse(testText);
        Assert.AreEqual("Hello world!", line.CleanText);
    }
}
