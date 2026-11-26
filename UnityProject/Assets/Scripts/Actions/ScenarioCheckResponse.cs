using System;

// Reply of /v1/scenario/check. checkpoint and name are the state after the check
[Serializable]
public class ScenarioCheckResponse
{
    public bool advanced;
    // True once the last checkpoint's condition is met
    public bool finished;
    public int checkpoint;
    public string name;
    public string reason;
    public int prompt_tokens;
    public int completion_tokens;
}
