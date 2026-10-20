using UnityEngine;

public class Talk : IAgentAction
{
    public string Name => "talk";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters == null || action.parameters.Length < 1) return "talk requires a message";

        string msg = action.parameters[0];
        // Empty addressee is the player, and a named nurse must exist
        string heardBy = string.IsNullOrEmpty(action.addressee) ? "user" : action.addressee;
        NPC other = null;
        if (heardBy != "user")
        {
            other = context.GetAgent(heardBy);
            if (other == null) return $"Couldn't find agent with name {heardBy}";
        }
        bool sendOn = action.isNextTurn && other != null;
        string speaker = action.agent;
        void start()
        {
            if (context.chatManager == null)
                Debug.LogWarning("Chat is null");
            else if (other == null)
            {
                Transform player = context.contextLibrary.Player;
                if (player != null)
                    agent.StartCoroutine(MovementActions.TurnTowards(agent.transform, player.position));
                context.chatManager.AddChat($"{speaker} to you: {msg}");
            }
            else
            {
                agent.StartCoroutine(MovementActions.TurnTowards(agent.transform, other.transform.position));
                context.chatManager.AddChat($"{speaker} to {heardBy}: {msg}");
            }
            if (sendOn)
                context.SendTalk(msg, heardBy, speaker);
        }

        result = new AnimAction(action, start, null, null);
        return null;
    }
}