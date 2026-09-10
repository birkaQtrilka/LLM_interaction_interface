import json
import os
from pathlib import Path

import httpx
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

load_dotenv(Path(__file__).resolve().parent / ".env")

PERSONA = "You are a scrub nurse in an operating room, reply in character, keep it short."
ACTION_INSTRUCTIONS = """Reply with a single JSON object with fields say and actions.
actions is an array of {name, parameters}. Parameters are strings.
Action list:
moveToSpot(spotName) — walk to a named spot
moveToPoint(x, y, z) — walk to coordinates
talk(msg) — say something in chat
"""

app = FastAPI()


class SendChatMessage(BaseModel):
    message: str
    world: str = ""


class ActionItem(BaseModel):
    name: str
    parameters: list[str] = Field(default_factory=list)


class GetMessage(BaseModel):
    say: str = ""
    actions: list[ActionItem] = Field(default_factory=list)


def build_messages(user_text: str, world: str) -> list[dict]:
    user = user_text
    if world:
        user = f"World:\n{world}\n\nUser request: {user_text}"
    return [
        {"role": "system", "content": f"{PERSONA}\n{ACTION_INSTRUCTIONS}"},
        {"role": "user", "content": user},
    ]


@app.post("/v1/turn", response_model=GetMessage)
def turn(body: SendChatMessage) -> GetMessage:
    api_key = os.environ.get("OPENAI_API_KEY")
    if not api_key:
        raise HTTPException(status_code=500, detail="OPENAI_API_KEY is not set")

    response = httpx.post(
        "https://api.openai.com/v1/chat/completions",
        headers={"Authorization": f"Bearer {api_key}"},
        json={
            "model": "gpt-4o-mini",
            "messages": build_messages(body.message, body.world),
            "response_format": {"type": "json_object"},
        },
        timeout=60,
    )
    if not response.is_success:
        raise HTTPException(status_code=response.status_code, detail=response.text)

    try:
        parsed = json.loads(response.json()["choices"][0]["message"]["content"])
    except (json.JSONDecodeError, KeyError, TypeError) as exc:
        raise HTTPException(status_code=502, detail=f"Model did not return JSON: {exc}") from exc

    actions = []
    for item in parsed.get("actions") or []:
        if not isinstance(item, dict):
            continue
        params = item.get("parameters") or []
        if not isinstance(params, list):
            params = [params]
        actions.append(
            ActionItem(name=str(item.get("name") or ""), parameters=[str(p) for p in params])
        )

    return GetMessage(say=str(parsed.get("say") or ""), actions=actions)
