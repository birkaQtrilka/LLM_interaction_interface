public interface IAgentAction
{
    string Name { get; }
    string TryBuild(ActionData action, AgentSystem context, NPC agent, out AnimAction result);
}