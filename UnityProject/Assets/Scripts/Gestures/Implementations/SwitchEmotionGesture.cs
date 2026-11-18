using System.Collections;
using System.Collections.Generic;

public class SwitchEmotionGesture : IGestureHandler
{
    public string Name => "changeEmotion";

    public string Layer => "emotion";

    public IEnumerator Execute(AgentSystem ctx, ActionData talkData, IReadOnlyDictionary<string, string> args, NPC agent)
    {
        agent.EmotionalStateHandler.SwitchToEmotionalState(args["emotion"]);
        yield break;
    }
}
