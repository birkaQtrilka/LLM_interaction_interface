using System.Collections;
using UnityEngine;

public static partial class Actions
{
    public static AnimAction Move(NPC agent, Vector3 pos, ActionData action)
    {

        Coroutine waitCr = null;
        void start()
        {
            agent.Anim.SetBool("Walking", true);
            agent.Nav.SetDestination(pos);
            agent.OnCollide += Agent_OnCollide;
        }
        IEnumerator WaitThenClear()
        {
            yield return Wait(agent);
            waitCr = null;
        }
        void Agent_OnCollide(Collision obj)
        {
            if (waitCr != null || !obj.collider.TryGetComponent<NPC>(out var other)) return;

            if (other.Nav.isStopped || !Utils.IsAgentMoving(other.Nav)) return;

            if(waitCr != null) agent.StopCoroutine(waitCr);
            waitCr = agent.StartCoroutine(WaitThenClear());
        }
        
        void end()
        {
            if (waitCr != null) agent.StopCoroutine(waitCr);
            waitCr = null;
            agent.OnCollide -= Agent_OnCollide;
            agent.Anim.SetBool("Walking", false);
            agent.Nav.ResetPath();
            agent.Nav.isStopped = false;
        }

        return new AnimAction(action, start, Utils.MonitorMovement(agent.Nav), end);
    }

    static IEnumerator Wait(NPC agent)
    {
        Debug.Log($"{agent.name} started wating");
        agent.Nav.isStopped = true;
        agent.Anim.SetBool("Walking", false);

        yield return new WaitForSeconds(3);
        agent.Nav.isStopped = false;
        Debug.Log($"{agent.name} stopped wating");
        agent.Anim.SetBool("Walking", true);
        //agent.Nav.Warp(agent.Nav.destination);
    }
}