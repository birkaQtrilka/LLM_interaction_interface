using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(MyTest), true)]
public class TestIsolator : Isolator<MyTest>
{
    protected override bool enabled => false;

}
