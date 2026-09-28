

using UnityEngine;

public static partial class Actions
{
    public static AnimAction Talk(ChatManager chat, string msg, ActionData action)
    {
        void start()
        {
            if (chat == null)
            {
                Debug.LogWarning("Chat is null");
                return;
            }

            chat.AddChat($"{action.agent}: {msg}");
        }

        return new AnimAction(action, start, null, null);
    }

}