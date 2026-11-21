using System.Collections;

/// <summary>
/// Abstraction over the LLM service. The real implementation is LLMBackend;
/// tests use a scripted mock.
/// </summary>
public interface ILLMBackend
{
    /// <summary>Round 1: decides which parts of the world the LLM needs to see.</summary>
    IEnumerator GetContext(string userPrompt, CoroutineResult<ContextQuery> res);

    /// <summary>Round 2: returns the action plan, given the serialized world state.</summary>
    IEnumerator GetActions(string userPrompt, string world, CoroutineResult<ActionsResponse> res,
        string addressee = null, string speaker = null);
}