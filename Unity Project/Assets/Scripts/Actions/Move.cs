using UnityEngine;

public static partial class Actions
{
    public static AnimAction Move(NPC agent, Vector3 pos, ActionData action)
    {

        void start()
        {
            agent.Anim.SetBool("Walking", true);
            agent.Nav.SetDestination(pos);
        }

        void end()
        {
            agent.Anim.SetBool("Walking", false);
        }

        return new AnimAction(action, start, Utils.MonitorMovement(agent.Nav), end);
    }
}