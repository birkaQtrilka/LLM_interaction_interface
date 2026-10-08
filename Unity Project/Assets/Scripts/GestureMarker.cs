using System.Collections.Generic;

public readonly struct GestureMarker
{
    public readonly int CharIndex;
    public readonly string Name;
    public readonly Dictionary<string, string> Args;
    public GestureMarker(int i, string n, Dictionary<string, string> a) { CharIndex = i; Name = n; Args = a; }
}
