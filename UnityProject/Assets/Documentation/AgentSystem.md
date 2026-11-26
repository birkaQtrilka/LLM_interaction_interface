# Agent System

Turns natural-language messages (from the player or from other NPCs) into a **plan** of
animations/actions executed by NPCs, using an LLM backend. Failed actions are reported back
to the LLM so it can correct its plan.

## Components

| Class | Responsibility |
|---|---|
| `AgentSystem` | Orchestrator. Runs turns, owns the busy lock, collects action errors, runs feedback turns. |
| `ILLMBackend` | Two-round LLM API. `AgentSystem` only knows this interface (`SetBackend` / `Init`). |
| `LLMBackend` | HTTP implementation (`/v1/context`, `/v1/turn`) plus session logging (`/v1/session/start|end|feedback|error`). Also forwards every `Debug.LogError` to the backend log. |
| `ContextLibrary` | Serializes world state (NPCs, spots, objects, playing animations, chat history) into the prompt text. Owns the chat history. |
| `AnimationLibrary` | Action registry (`IAgentAction`), scheduler and plan cancellation. |
| `IAgentAction` | One action type (e.g. `grab`). Validates parameters and builds the behavior. |
| `ChatManager`, `LookCast` | UI input; `LookCast` picks the addressee by what the player is looking at. |

## Data types

* `ContextQuery` (struct): what round 1 asks for (`getSpots`, `getObjects`, per-object `ObjectFlags`).
  `ContextQuery.GetFullContext()` asks for everything and is used when `sendAllContext` is on.
* `ActionData`: `id`, `name`, `agent`, `parameters`, `runAfter` (ids), `delayBefore` (seconds).
* `ActionsResponse`: `actions[]` plus token counts. This is one **plan**.
* `CoroutineResult<T>`: result holder for coroutines. Always complete it with `SetResult` or `SetError`
  (both set `IsDone`).

## Turn lifecycle

```
user message / NPC line
        │
   RunSystem / RunHeardTurn
        │ turnInProgress = true
        ├─ Round 1: backend.GetContext(prompt) -> ContextQuery   (skipped if sendAllContext)
        ├─ remember turn (request, query, addressee, speaker); reset feedback budget; clear pending errors
        └─ Round 2: contextLibrary.GetContext(query) -> world string
                    backend.GetActions(prompt, world, addressee, speaker) -> ActionsResponse
                         │
                         ├─ store the player's line in chat history (not for feedback turns or NPC speech)
                         ├─ pendingErrors.Clear()    (a new plan supersedes errors of the old one)
                         └─ ActionsSuccess: PlayAnimation for each action (new planId)
```

Both rounds run inside `RunBusy`, which holds `IsBusy` (disables chat input, shows the loading
animator). A second message while busy is rejected with a warning. `IsBusy` only covers the backend
calls; the *actions* keep running afterwards.

## Plans, ids and scheduling

* Every round-2 response is a plan with its own `planId` (`++planCounter`).
* Ids must be unique among **live** animations. A duplicate is rejected ("Action with id N already
  exists") and reported as an error.
* `runAfter` is satisfied when the referenced id is **no longer in the animation list**. Consequences:
  an id that never existed (or already finished) counts as done, and a self-reference or cycle waits forever.
* `delayBefore` counts down in `AnimationManagerCoroutine` and mutates `ActionData.delayBefore`.
* The manager ticks every frame: removes finished animations, starts those whose dependencies are gone
  and whose delay elapsed.
* Animation lifecycle: `start()` -> `behavior` -> `end()` -> `isFinished`.
* `StartCoroutine` runs the coroutine **synchronously up to its first `yield`**, before
  `anim.coroutine` is assigned. This is why behaviors begin with `yield return null`.

## Error handling and feedback loop

1. An action fails in one of two ways:
   * **Build-time:** `TryBuild` (or the duplicate-id check) returns a string. `ActionsSuccess` stops at the
     first failure; later actions of the plan are never queued.
   * **Run-time:** the behavior calls `context.ReportActionError(msg, agent, action)`.
2. `ReportError` builds `"[agent] message (failed action: <json>)"` and calls `CancelPlan(planId)`.
   Every unfinished action of the same plan is stopped and listed as "was running" or "had not started".
   `end()` runs only for actions that had started.
3. Messages are de-duplicated and queued in `pendingErrors`. `Update()` waits until the system is idle
   (no turn in progress, not busy) and `errorBatchDelay` has passed. `RunFeedbackTurn` then sends
   original request + errors + "Provide a new plan" with the **same context query, addressee and speaker**.
4. At most `maxFeedbackRounds` consecutive feedback rounds per real turn; then it gives up with a warning.
   A new real turn resets the budget.
5. Feedback turns are not written to chat history.
6. Errors reported before any turn has happened are never sent (there is nothing to replay).

## Robustness rules

* Scene objects can be destroyed at any time. `ContextLibrary.GetContext` prunes destroyed items from
  `environment`, `spots` and `agents`, and the lookups (`GetObject`, `GetAgent`) skip them. Never touch
  `item.transform.name` without a null check.
* Backend failures (network, invalid JSON) must complete the `CoroutineResult` with `SetError`.
  `AgentSystem` logs them, posts them to the chat and releases the busy lock.

## Writing an action

Implement `IAgentAction` (parameterless constructor; discovered by reflection; names must be unique):

```csharp
public string Name => "my_action";
public string TryBuild(ActionData a, AgentSystem ctx, NPC agent, out AnimAction result)
```

Rules:
1. **Validate everything in `TryBuild`** and return a descriptive error string. The LLM reads it.
2. Start the behavior with `yield return null` (see "Plans, ids and scheduling").
3. Report run-time failures with `ReportActionError(msg, agent, action)`, then `yield break`.
   C# forbids `yield return` inside try/catch, so step inner enumerators manually, like `Grab`.
4. Use `end()` for cleanup. It runs when the action finishes **and** when it is cancelled while running;
   it does not run if the action never started.
5. Never call `CancelPlan` (or `ReportActionError`) from `start()`: it runs inside the scheduler loop.
6. Error text should be actionable for an LLM (what, which object, why).
7. Pass the `ActionData` instance you were given into `AnimAction`, so error reports can find the plan.

## Testing

* Inject a fake backend: `agentSystem.SetBackend(new MockLLMBackend())` or `Init(mock, anim, ctx)`.
* `Configure(maxFeedbackRounds, errorBatchDelay)` makes feedback fast in tests.
* Tests live in `Assets/Tests/PlayMode`. `AgentRig.Create()` builds a full system: mock backend, runtime
  NavMesh floor and an NPC from `AgentRig.NpcPrefabPath`. Create the NPC *after* `ContextLibrary`, because
  its `Awake` auto-registers NPCs it finds in the scene.
* Test-only actions `test_wait` and `test_fail` register themselves automatically.
* Only errors and exceptions fail a Unity test; use `LogAssert.Expect` for expected `LogError`s.