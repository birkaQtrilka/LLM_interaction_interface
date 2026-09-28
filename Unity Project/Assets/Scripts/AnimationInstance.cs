using System;
using System.Collections;

[Serializable]
public class AnimationInstance
{
    public ActionData data;
    public IEnumerator behavior;
    public Action start;
    public Action end;

    public bool isPlaying;
    public bool isFinished;

    public AnimationInstance(ActionData data, IEnumerator behavior, Action start, Action end)
    {
        this.data = data;
        this.behavior = behavior;
        this.start = start;
        this.end = end;
        this.isPlaying = false;
        this.isFinished = false;
    }

    private AnimationInstance() { }

    public override string ToString()
    {
        string dependencies = data.runAfter != null ? string.Join(", ", data.runAfter) : "";

        return $"Animation: {data?.name}, ID: {data?.id}, isPlaying: {isPlaying}, dependentOn: [{dependencies}], delayBefore: {data?.delayBefore}";
    }

}
