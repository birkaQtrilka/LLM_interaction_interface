using NUnit.Framework;
using System.Linq;
using UnityEngine;

public class LineParserTest 
{
    public string testText = "Hi there![wave] I'm happy to see so [all] many people gathering to support a [nod] good cause. Special thanks [point target=NPC1] to John for organizing the food booth";

    [Test]
    public void Clean_Text_Test()
    {
        string testText = "Hello[wave] world[nod]!";
        ParsedLine line = LineParser.Parse(testText);
        Assert.AreEqual("Hello world!", line.CleanText);
    }

    [Test]
    public void Correct_Commands_Returned_Test()
    {
        string testText = "Hello[wave] world[point target=NPC1]!";
        ParsedLine line = LineParser.Parse(testText);
        Assert.AreEqual("Hello world!", line.CleanText);
        CollectionAssert.AreEqual(new string[] { "wave", "point" }, line.Markers.ConvertAll(m => m.Name));
        CollectionAssert.AreEqual(
            new[] { "target", "NPC1" },
            line.Markers
                .SelectMany(m => m.Args.SelectMany(kvp => new[] { kvp.Key, kvp.Value }))
                .ToArray()
        );
    }
}
