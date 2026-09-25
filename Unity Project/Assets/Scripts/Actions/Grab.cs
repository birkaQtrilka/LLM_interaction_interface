public static partial class Actions
{
    // now this is primitive, but it will do for now. We can improve this later with IK and other techniques.
    public static AnimAction Grab(NPC agent, ContextItem item, ActionData action)
    {
        Flag grabbed = new();
        void start()
        {
            agent.Anim.SetTrigger("Grab");
            agent.GrabReceiver.OnGrabPoint += snapObjectToHand;
        }

        void snapObjectToHand()
        {
            agent.GrabItem(item.transform, true);
            grabbed.value = true;
        }

        void end()
        {
            agent.GrabReceiver.OnGrabPoint -= snapObjectToHand;
        }

        return new AnimAction(action, start, Utils.MonitorFlag(grabbed), end);
    }
}
