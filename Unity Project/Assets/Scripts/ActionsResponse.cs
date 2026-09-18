using System;

[Serializable]
public class ActionsResponse
{
    public ActionData[] actions;
    public int prompt_tokens;
    public int completion_tokens;
}
