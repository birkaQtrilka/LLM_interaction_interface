import json
import os
from pathlib import Path

import httpx
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException

from schemas import (
    ActionsRequestBody, ContextRequestBody, ContextQuery, 
    ActionsResponse, ActionData, ObjectFlags, UserFlags
)
from prompts import PERSONA, CONTEXT_CATALOG, get_system_prompt
BASE_DIR = Path(__file__).resolve().parent
load_dotenv(BASE_DIR / ".env")

with open(BASE_DIR / "actions.json", "r") as f:
    ACTIONS = json.load(f)

app = FastAPI()

def openai_json(messages: list[dict]) -> tuple[dict, dict]:
    api_key = os.environ.get("OPENAI_API_KEY")
    if not api_key:
        raise HTTPException(status_code=500, detail="OPENAI_API_KEY is not set")

    response = httpx.post(
        "https://api.openai.com/v1/chat/completions",
        headers={"Authorization": f"Bearer {api_key}"},
        json={
            "model": "gpt-4o-mini",
            "messages": messages,
            "response_format": {"type": "json_object"},
        },
        timeout=60,
    )
    if not response.is_success:
        raise HTTPException(status_code=response.status_code, detail=response.text)

    payload = response.json()
    try:
        parsed = json.loads(payload["choices"][0]["message"]["content"])
    except (json.JSONDecodeError, KeyError, TypeError) as exc:
        raise HTTPException(status_code=502, detail=f"Model did not return JSON: {exc}") from exc
    if not isinstance(parsed, dict):
        raise HTTPException(status_code=502, detail="Model JSON was not an object")
    return parsed, payload.get("usage") or {}


def flags_from(data: object) -> dict:
    if not isinstance(data, dict):
        return {}
    return data




def build_messages(user_text: str, world: str) -> list[dict]:
    user = f"Context:\n{world}\n\nUser request: {user_text}"
        
    action_lines = []
    for action in ACTIONS:
        action_lines.append(f"// {action['doc']}\n{action['name']}({action['args']})")
    
    actions_str = "\n".join(action_lines)
    print("------------- SYSTEM -------------------")

    system = get_system_prompt(PERSONA, actions_str)
    print(system);
    print("------------- USER -------------------")
    print(user)
    return [
        {"role": "system", "content": system},
        {"role": "user", "content": user},
    ]

# todo: have serialization/deserialization and object definition of ContextQuery in one spot 
@app.post("/v1/context", response_model=ContextQuery)
def context(body: ContextRequestBody) -> ContextQuery:
    parsed, usage = openai_json(
        [
            {"role": "system", "content": CONTEXT_CATALOG},
            {"role": "user", "content": body.message},
        ]
    )
    user = flags_from(parsed.get("userFlags"))
    objects = flags_from(parsed.get("objectFlags"))
    return ContextQuery(
        getSpots=bool(parsed.get("getSpots")),
        getObjects=bool(parsed.get("getObjects")),
        objectFlags=ObjectFlags(
            position=bool(objects.get("position")),
            rotation=bool(objects.get("rotation")),
            description=bool(objects.get("description")),
            bounds=bool(objects.get("bounds")),
            neighbours=bool(objects.get("neighbours")),
        ),
        userFlags=UserFlags(
            position=bool(user.get("position")),
            rotation=bool(user.get("rotation")),
            neighbours=bool(user.get("neighbours")),
        ),
        prompt_tokens=int(usage.get("prompt_tokens") or 0),
        completion_tokens=int(usage.get("completion_tokens") or 0),
    )


@app.post("/v1/turn", response_model=ActionsResponse)
def turn(body: ActionsRequestBody) -> ActionsResponse:
    parsed, usage = openai_json(build_messages(body.message, body.world))

    actions = []
    print("-----------------ACTIONS-----------------")
    print(parsed)
    for item in parsed.get("actions") or []:
        if not isinstance(item, dict):
            continue
        params = item.get("parameters") or []
        if not isinstance(params, list):
            params = [params]
            
        run_after = item.get("runAfter") or []
        if not isinstance(run_after, list):
            run_after = [run_after]
            
        actions.append(
            ActionData(
                id=int(item.get("id", 0)),
                name=str(item.get("name") or ""),
                agent=str(item.get("agent") or "AAAA"),
                parameters=[str(p) for p in params],
                runAfter=[int(r) for r in run_after],
                delayBefore=float(item.get("delayBefore", 0.0))
            )
        )

    return ActionsResponse(
        actions=actions,
        prompt_tokens=int(usage.get("prompt_tokens") or 0),
        completion_tokens=int(usage.get("completion_tokens") or 0),
    )