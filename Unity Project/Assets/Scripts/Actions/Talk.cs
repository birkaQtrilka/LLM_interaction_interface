

public static partial class Actions
{
    public static AnimAction Talk(ChatManager chat, string msg, ActionData action)
    {
        void start()
        {
            chat.AddChat(msg);
        }

        return new AnimAction(action, start, null, null);
    }

}