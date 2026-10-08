using UnityEngine;

public class Talk : IAgentAction
{
    public string Name => "talk";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters == null || action.parameters.Length < 1) return "talk requires a message";

        string msg = action.parameters[0];
        // The model often returns only the sentence, and that sentence is for the user
        string receiver = action.parameters.Length > 1 ? action.parameters[1] : "user";
        void start()
        {
            if (context.chatManager == null)
            {
                Debug.LogWarning("Chat is null");
                return;
            }
            if (receiver == "user")
            {
                receiver = "you";
                Transform player = context.contextLibrary.Player;
                if (player != null)
                    agent.StartCoroutine(MovementActions.TurnTowards(agent.transform, player.position));
            }
            else
            {
                agent.StartCoroutine(MovementActions.TurnTowards(agent.transform, context.GetAgent(receiver).transform.position));
            }
            context.chatManager.AddChat($"{action.agent} to {receiver}: {msg}");

        }

        result = new AnimAction(action, start, null, null);
        return null;
    }
}