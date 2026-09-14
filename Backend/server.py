import json
import os
from pathlib import Path

import httpx
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

load_dotenv(Path(__file__).resolve().parent / ".env")

PERSONA = "You are a scrub nurse in an operating room, reply in character, keep it short."
# Adding a verb later is one more row. build_messages turns this into prompt text.
ACTIONS = [
    {"name": "moveToSpot", "args": "spotName", "doc": "walk to a named spot"},
    {"name": "moveToPoint", "args": "x, y, z", "doc": "walk to coordinates"},
    {"name": "talk", "args": "msg", "doc": "say something in chat"},
    {"name": "lookAt", "args": "targetName", "doc": "face a named spot or object"},
    {"name": "grab", "args": "objectName", "doc": "pick up a named object from the environment"},
]

# Round-1 catalog: flags only, no coordinates and no action names
CONTEXT_CATALOG = """You pick which world data is needed for the user request.
Reply with a single JSON object that uses every field in this schema. Every value is a boolean.
Do not add actions, names, or coordinates.
{
  "getSpots": false,
  "getUser": {
    "position": false,
    "rotation": false,
    "neighbours": false
  },
  "getObjects": {
    "position": false,
    "rotation": false,
    "description": false,
    "neighbours": false
  }
}
Set a field true only if that data is needed.
"""

app = FastAPI()


class SendChatMessage(BaseModel):
    message: str
    # Unity sends a snapshot object; curl can still send a string
    world: str | dict = ""


class ContextRequest(BaseModel):
    message: str


class UserFlags(BaseModel):
    position: bool = False
    rotation: bool = False
    neighbours: bool = False


class ObjectFlags(BaseModel):
    position: bool = False
    rotation: bool = False
    description: bool = False
    neighbours: bool = False


class ContextQuery(BaseModel):
    getSpots: bool = False
    getUser: UserFlags = Field(default_factory=UserFlags)
    getObjects: ObjectFlags = Field(default_factory=ObjectFlags)
    prompt_tokens: int = 0
    completion_tokens: int = 0


class ActionItem(BaseModel):
    name: str
    parameters: list[str] = Field(default_factory=list)


class GetMessage(BaseModel):
    say: str = ""
    actions: list[ActionItem] = Field(default_factory=list)
    prompt_tokens: int = 0
    completion_tokens: int = 0


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


def world_to_text(world: str | dict) -> str:
    if isinstance(world, str):
        return world

    spot_parts = []
    for spot in world.get("spots") or []:
        pos = spot.get("position") or {}
        spot_parts.append(f"{spot.get('name')}: ({pos.get('x')}, {pos.get('y')}, {pos.get('z')})")

    npc = world.get("npc") or {}
    pos = npc.get("position") or {}
    rot = npc.get("rotation") or {}

    item_parts = []
    for item in world.get("environment") or []:
        item_pos = item.get("position") or {}
        item_parts.append(
            f"{item.get('name')}: ({item_pos.get('x')}, {item_pos.get('y')}, {item_pos.get('z')})"
        )

    return (
        # TODO: Make it dynamic to not send empty lines if there are no spots or items
        "These are all the spot positions in the digital world: "
        + ", ".join(spot_parts)
        + f"\nThis is your NPC data: position: ({pos.get('x')}, {pos.get('y')}, {pos.get('z')}), "
        + f"rotation: ({rot.get('x')}, {rot.get('y')}, {rot.get('z')}, {rot.get('w')})"
        + "\nThese are objects you can grab: "
        + ", ".join(item_parts)
    )


def build_messages(user_text: str, world: str | dict) -> list[dict]:
    user = user_text
    if world:
        user = f"World:\n{world_to_text(world)}\n\nUser request: {user_text}"
    action_lines = []
    for action in ACTIONS:
        action_lines.append(f"{action['name']}({action['args']}) — {action['doc']}")
    system = (
        f"{PERSONA}\n"
        "Reply with a single JSON object with fields say and actions.\n"
        "actions is an array of {name, parameters}. Parameters are strings.\n"
        "Action list:\n"
        + "\n".join(action_lines)
    )
    return [
        {"role": "system", "content": system},
        {"role": "user", "content": user},
    ]


@app.post("/v1/context", response_model=ContextQuery)
def context(body: ContextRequest) -> ContextQuery:
    parsed, usage = openai_json(
        [
            {"role": "system", "content": CONTEXT_CATALOG},
            {"role": "user", "content": body.message},
        ]
    )
    user = flags_from(parsed.get("getUser"))
    objects = flags_from(parsed.get("getObjects"))
    return ContextQuery(
        getSpots=bool(parsed.get("getSpots")),
        getUser=UserFlags(
            position=bool(user.get("position")),
            rotation=bool(user.get("rotation")),
            neighbours=bool(user.get("neighbours")),
        ),
        getObjects=ObjectFlags(
            position=bool(objects.get("position")),
            rotation=bool(objects.get("rotation")),
            description=bool(objects.get("description")),
            neighbours=bool(objects.get("neighbours")),
        ),
        prompt_tokens=int(usage.get("prompt_tokens") or 0),
        completion_tokens=int(usage.get("completion_tokens") or 0),
    )


@app.post("/v1/turn", response_model=GetMessage)
def turn(body: SendChatMessage) -> GetMessage:
    parsed, usage = openai_json(build_messages(body.message, body.world))

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

    # Token counts live on the provider payload, not in the model's say/actions JSON
    return GetMessage(
        say=str(parsed.get("say") or ""),
        actions=actions,
        prompt_tokens=int(usage.get("prompt_tokens") or 0),
        completion_tokens=int(usage.get("completion_tokens") or 0),
    )
