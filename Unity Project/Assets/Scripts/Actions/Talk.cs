using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Talk : IAgentAction
{
    public string Name => "talk";

    struct HeardLine
    {
        public string message;
        public string addressee;
        public string speaker;
    }

    static readonly Queue<HeardLine> pendingTalks = new();
    static bool drainingTalks;

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        result = default;
        if (action.parameters.Length < 2) return "talk requires 3 parameters: message, receiver";

        string msg = action.parameters[0];
        // parameters: [message, addressee?, isNextTurn?]. Empty or missing addressee is the player
        string heardBy = action.parameters.Length > 1 && !string.IsNullOrEmpty(action.parameters[1])
            ? action.parameters[1]
            : "user";
        bool isNextTurn = action.parameters.Length > 2
            && action.parameters[2].Trim().Equals("true", System.StringComparison.OrdinalIgnoreCase);

        NPC other = null;
        if (heardBy != "user")
        {
            other = context.GetAgent(heardBy);
            if (other == null) return $"Couldn't find agent with name {heardBy}";
        }
        bool sendOn = isNextTurn && other != null;
        string speaker = action.agent;
        void start()
        {
            string receiver = "you";
            if (other == null)
            {
                Transform player = context.contextLibrary.Player;
                if (player != null) agent.StartCoroutine(MovementActions.TurnTowards(agent, player.position));
                else Debug.LogWarning("Player transform is null");
            }
            else
            {
                receiver = other.name;
                agent.StartCoroutine(MovementActions.TurnTowards(agent, other.transform.position));
            }
            ParsedLine line = LineParser.Parse(msg);
            
            if (context.chatManager == null) context.chatManager.AddChat($"{action.agent} to {receiver}: {line.CleanText}");

            if (context.AnimationLibrary.DialoguePlayer == null)
                Debug.LogWarning("DialoguePlayer is null");
            else 
                agent.StartCoroutine( context.AnimationLibrary.DialoguePlayer.Play(line, context, action) );
            if (sendOn) EnqueueHeard(context, msg, heardBy, speaker);

        }

        result = new AnimAction(action, start, null, null);
        return null;
    }

    // Queues a line so the addressee runs a turn after the current one finishes
    static void EnqueueHeard(AgentSystem context, string message, string addressee, string speaker)
    {
        pendingTalks.Enqueue(new HeardLine { message = message, addressee = addressee, speaker = speaker });
        if (!drainingTalks)
            context.StartCoroutine(DrainTalks(context));
    }

    static IEnumerator DrainTalks(AgentSystem context)
    {
        drainingTalks = true;
        while (pendingTalks.Count > 0)
        {
            while (context.IsBusy)
                yield return null;
            HeardLine line = pendingTalks.Dequeue();
            yield return context.RunHeardTurn(line.message, line.addressee, line.speaker);
        }
        drainingTalks = false;
    }
}
