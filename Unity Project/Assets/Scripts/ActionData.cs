using System;

[Serializable]
public class ActionData
{
    public int id;
    public string name;
    public string[] parameters;
    public int[] runAfter;
    public float delayBefore;
}
