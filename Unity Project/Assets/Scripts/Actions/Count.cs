
using System.Collections;
using UnityEngine;

public static partial class Actions
{
    public static AnimAction Count(ChatManager chat, int total, ActionData action)
    {
        IEnumerator behavior()
        {
            int count = 0;

            while (count <= total)
            {
                chat.AddChat($"{action.agent}: Count- {count++}");

                yield return new WaitForSeconds(1f);
            }
        }

        return new AnimAction(action, null, behavior(), null);
    }

}