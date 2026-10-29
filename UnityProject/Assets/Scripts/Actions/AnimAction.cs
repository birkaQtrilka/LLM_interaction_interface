using System;
using System.Collections;

public readonly struct AnimAction
{
    public readonly Action start;
    public readonly IEnumerator behavior;
    public readonly Action end;
    public readonly ActionData data;

    public AnimAction(ActionData data, Action start, IEnumerator behavior, Action end)
    {
        this.data = data;
        this.start = start;
        this.behavior = behavior;
        this.end = end;
    }
}
