using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ForceRelease : IAgentAction
{
    public string Name => "forceRelease";

    public string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result)
    {
        void start() {
            agent.Hand.ForceRelease();
        }
        result = new AnimAction(action, start, null, null);
        return null;
    }
}