using UnityEngine;

public class Talk : IAgentAction
{
    public string Name => "talk";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length < 1) return "talk requires 1 parameter: message";

        string msg = action.parameters[0];
        void start()
        {
            if (context.chatManager == null)
            {
                Debug.LogWarning("Chat is null");
                return;
            }
            context.chatManager.AddChat($"{action.agent}: {msg}");
        }

        result = new AnimAction(action, start, null, null);
        return null;
    }
}