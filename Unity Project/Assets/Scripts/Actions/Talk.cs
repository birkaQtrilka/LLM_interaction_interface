using UnityEngine;

public class Talk : IAgentAction
{
    public string Name => "talk";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length < 2) return "talk requires 2 parameters: message, receiver";

        string msg = action.parameters[0];
        string receiver = action.parameters[1];
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
                agent.StartCoroutine(MovementActions.TurnTowards(agent.transform, context.contextLibrary.Player.position));
            }
            else
            {
                agent.StartCoroutine(MovementActions.TurnTowards(agent.transform, context.GetAgent(receiver).transform.position));
            }
            ParsedLine line = LineParser.Parse(msg);
            
            context.chatManager.AddChat($"{action.agent} to {receiver}: {line.CleanText}");
            if(context.AnimationLibrary.DialoguePlayer == null)
            {
                Debug.LogWarning("DialoguePlayer is null");
                return;
            }
            agent.StartCoroutine( context.AnimationLibrary.DialoguePlayer.Play(line, context, action) );

        }

        result = new AnimAction(action, start, null, null);
        return null;
    }
}