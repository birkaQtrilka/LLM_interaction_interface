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
        if (action.parameters == null || action.parameters.Length != 3)
            return "talk requires 3 parameters: message, addressee, isNextTurn";

        string msg = action.parameters[0];
        if (string.IsNullOrWhiteSpace(msg))
            return "talk requires a non-empty message";

        // parameters: [message, addressee, isNextTurn]. Empty addressee is the player
        string heardBy = !string.IsNullOrEmpty(action.parameters[1])
            ? action.parameters[1]
            : "user";
        bool isToUser = heardBy.Equals("user", System.StringComparison.OrdinalIgnoreCase);
        if (isToUser) heardBy = "user";

        bool isNextTurn = !string.IsNullOrEmpty(action.parameters[2])
            && action.parameters[2].Trim().Equals("true", System.StringComparison.OrdinalIgnoreCase);

        NPC other = null;
        if (!isToUser)
        {
            other = context.GetAgent(heardBy);
            if (other == null) return $"Couldn't find agent with name '{heardBy}'";
        }

        bool sendOn = isNextTurn && other != null;
        string speaker = action.agent;
        string error = null;

        void start()
        {
            if (!isToUser && other == null)
            {
                error = $"Cannot talk to '{heardBy}': agent no longer exists.";
                return;
            }

            string receiver = "you";
            if (isToUser)
            {
                Transform player = context.contextLibrary.Player;
                if (player != null) MovementActions.StartTurn(agent, player.position);
                else Debug.LogWarning("Player transform is null");
            }
            else
            {
                receiver = other.name;
                MovementActions.StartTurn(agent, other.transform.position);
            }

            ParsedLine line = LineParser.Parse(msg);
            string header = $"<b>{action.agent}</b> to {receiver}: ";

            // CleanText has the gesture tags removed, so they are not fed back to the model
            context.contextLibrary.AddMessageToHistory(speaker, heardBy, line.CleanText);

            // DialoguePlayer types the line into the chat, without it the whole line is shown at once
            if (context.AnimationLibrary == null || context.AnimationLibrary.DialoguePlayer == null)
            {
                Debug.LogWarning("DialoguePlayer is null, so the line is shown without typing or gestures");
                if (context.chatManager != null) context.chatManager.AddChat(header + ChatMessage.Plain(line.CleanText));
            }
            else
                agent.StartCoroutine(context.AnimationLibrary.DialoguePlayer.Play(line, context, action, agent, header));

            if (sendOn) EnqueueHeard(context, msg, heardBy, speaker);
        }

        IEnumerator behavior()
        {
            if (error == null) yield break;
            yield return null;
            context.ReportActionError(error, agent, action);
        }

        result = new AnimAction(action, start, behavior(), null);
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
        try
        {
            while (pendingTalks.Count > 0)
            {
                while (context.IsBusy)
                    yield return null;
                HeardLine line = pendingTalks.Dequeue();
                yield return context.RunHeardTurn(line.message, line.addressee, line.speaker);
            }
        }
        finally
        {
            drainingTalks = false;
        }
    }
}