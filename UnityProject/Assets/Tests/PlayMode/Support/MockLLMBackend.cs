using System;
using System.Collections;
using System.Collections.Generic;

public class MockLLMBackend : ILLMBackend
{
    public class ActionsCall
    {
        public string Prompt, World, Addressee, Speaker;
    }

    // What the system sent us
    public readonly List<string> ContextPrompts = new();
    public readonly List<ActionsCall> ActionCalls = new();

    // What we answer
    public ContextQuery ContextToReturn;                // default struct: everything off
    public string ContextError;                         // set to make round 1 fail
    public int DelayFrames;                             // simulate network latency
    public Func<ActionsResponse> Fallback = () => new ActionsResponse { actions = new ActionData[0] };

    readonly Queue<(ActionsResponse resp, string error)> script = new();

    public void Enqueue(ActionsResponse resp) => script.Enqueue((resp, null));
    public void EnqueueError(string error) => script.Enqueue((null, error));

    public IEnumerator GetContext(string userPrompt, CoroutineResult<ContextQuery> res)
    {
        ContextPrompts.Add(userPrompt);
        for (int i = 0; i < DelayFrames; i++) yield return null;

        if (ContextError != null) res.SetError(ContextError);
        else res.SetResult(ContextToReturn);
    }

    public IEnumerator GetActions(string userPrompt, string world, CoroutineResult<ActionsResponse> res,
        string addressee = null, string speaker = null)
    {
        ActionCalls.Add(new ActionsCall { Prompt = userPrompt, World = world, Addressee = addressee, Speaker = speaker });
        for (int i = 0; i < DelayFrames; i++) yield return null;

        if (script.Count == 0) { res.SetResult(Fallback()); yield break; }

        var next = script.Dequeue();
        if (next.error != null) res.SetError(next.error);
        else res.SetResult(next.resp);
    }
}