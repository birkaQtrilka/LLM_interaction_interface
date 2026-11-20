using System.Linq;
using UnityEngine;

public class ReachableTest : MyTest
{
    public string[] itemNames;
    public ContextLibrary contextLibrary;

    private void OnEnable()
    {
        ContextItem[] items = itemNames.Select(n=> contextLibrary.environment.FirstOrDefault(x=>x.GetName() == n)).ToArray();
        foreach (var item in items)
        {
            contextLibrary.GetItemData(item, new(false, false, false, false, false, true));

        }
    }
}
