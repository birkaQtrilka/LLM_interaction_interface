from typing import Callable

from prompts import get_judge_prompt

# Takes chat messages and returns (parsed JSON reply, token usage). The runner does not know which provider answers
AskModel = Callable[[list[dict]], tuple[dict, dict]]


def build_judge_messages(scenario: dict, checkpoint_id: int, world: str, finished_actions: list[str]) -> list[dict]:
    checkpoint = scenario["checkpoints"][checkpoint_id - 1]
    if finished_actions:
        finished = "\n".join(f"- {a}" for a in finished_actions)
    else:
        finished = "(none)"
    user = f"Context:\n{world}\n\nActions finished since this state began:\n{finished}"
    return [
        {"role": "system", "content": get_judge_prompt(scenario, checkpoint)},
        {"role": "user", "content": user},
    ]


def judge(ask: AskModel, messages: list[dict]) -> tuple[bool, str, dict]:
    """Asks the model if advances_when is met. Returns (met, reason, usage). Raises ValueError on a malformed reply"""
    parsed, usage = ask(messages)
    met = parsed.get("met")
    reason = parsed.get("reason")
    # Strict on purpose: a missing or non-boolean answer is an error, not a quiet "no"
    if not isinstance(met, bool):
        raise ValueError(f"Judge reply has no boolean 'met': {parsed}")
    if not isinstance(reason, str) or not reason.strip():
        raise ValueError(f"Judge reply has no 'reason': {parsed}")
    return met, reason.strip(), usage
