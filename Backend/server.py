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
    {"name": "moveToSpot", "args": "spotName: string", "doc": "when user asks you to move to a specific spot in the world, use this action. The parameter is the name of the spot."},
    {"name": "moveToPoint", "args": "x: float, y: float, z: float", "doc": "when the user asks you to go to an arbitrary point in the world, use this action. The parameters are the x, y, and z coordinates of the point."},
    {"name": "talk", "args": "msg: string", "doc": "use this action when the user asks you something or where a comment to some other action is appropriate. The parameter is the text you want to say."},
    {"name": "lookAt", "args": "targetName: string", "doc": "face a named spot or object"},
    {"name": "grab", "args": "objectName: string", "doc": "pick up a named object from the environment"},
]

app = FastAPI()

class ActionsRequestBody(BaseModel):
    message: str
    # Unity sends a snapshot object; curl can still send a string
    world: str | dict = ""

class ContextRequestBody(BaseModel):
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
    getObjects: bool = False
    objectFlags: ObjectFlags = Field(default_factory=ObjectFlags)
    userFlags: UserFlags = Field(default_factory=UserFlags)
    prompt_tokens: int = 0
    completion_tokens: int = 0


# Dynamically generate the JSON structure from the Pydantic model.
# We exclude prompt_tokens and completion_tokens because the LLM doesn't supply them.
schema_json_string = ContextQuery().model_dump_json(
    exclude={"prompt_tokens", "completion_tokens"}, 
    indent=2
)

# Round-1 catalog: flags only, no coordinates and no action names
CONTEXT_CATALOG = f"""You pick which world data is needed for the user request.
Reply with a single JSON object that uses every field in this schema. Every value is a boolean.
Do not add actions, names, or coordinates.
{schema_json_string}
Set a field true only if that data is needed.
"""

class ActionData(BaseModel):
    id: int = 0
    name: str
    parameters: list[str] = Field(default_factory=list)
    runAfter: list[int] = Field(default_factory=list)
    delayBefore: float = 0.0


class ActionsResponse(BaseModel):
    actions: list[ActionData] = Field(default_factory=list)
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

    output_lines = []

    spot_parts = []
    for spot in world.get("spots") or []:
        pos = spot.get("position") or {}
        spot_parts.append(f"{spot.get('name')}: ({pos.get('x')}, {pos.get('y')}, {pos.get('z')})")
        
    if spot_parts:
        output_lines.append("These are all the spot positions in the digital world: " + ", ".join(spot_parts))

    npc = world.get("npc") or {}
    if npc:
        pos = npc.get("position") or {}
        rot = npc.get("rotation") or {}
        output_lines.append(
            f"This is your NPC data: position: ({pos.get('x')}, {pos.get('y')}, {pos.get('z')}), "
            f"rotation: ({rot.get('x')}, {rot.get('y')}, {rot.get('z')}, {rot.get('w')})"
        )

    item_parts = []
    for item in world.get("environment") or []:
        item_pos = item.get("position") or {}
        item_parts.append(
            f"{item.get('name')}: ({item_pos.get('x')}, {item_pos.get('y')}, {item_pos.get('z')})"
        )
        
    if item_parts:
        output_lines.append("These are objects you can grab: " + ", ".join(item_parts))

    return "\n".join(output_lines)


def build_messages(user_text: str, world: str | dict) -> list[dict]:
    user = user_text
    if world:
        user = f"Context:\n{world_to_text(world)}\n\nUser request: {user_text}"
        
    action_lines = []
    for action in ACTIONS:
        # Formatted to match Unity: "// comment \n actionName(params)"
        action_lines.append(f"// {action['doc']}\n{action['name']}({action['args']})")
    
    actions_str = "\n".join(action_lines)

    # Added double curly braces {{ }} inside the f-string where actual JSON brackets are needed
    system = f"""{PERSONA} You will be given context about the world and the user will ask you tasks/questions related to the context.
Below are the actions you can perform to achieve the task / answer the question asked by the user along with documentation about when to use it. 

Action List:
{actions_str}

You can sequence these actions using the 'id', 'runAfter', and 'delayBefore' properties.
- To play an action immediately, leave 'runAfter' empty and 'delayBefore' at 0.
- To play actions at the same time, give them the same 'runAfter' array and the same 'delayBefore'.
- To play an action after another action finishes, add the previous action's 'id' to the 'runAfter' array.
- To play an action after another action finishes + n seconds, use 'runAfter' with the previous action's 'id' and set 'delayBefore' to n.
- To play an action after n seconds from the start, leave 'runAfter' empty and set 'delayBefore' to n.

You will get pending animations from the user, do not repeat IDs.
You MUST respond ONLY with a valid JSON object in the exact format shown below. Do not add any conversational text or markdown before or after the JSON.

Format:
{{
  "actions": [
    {{
      "id": 0,
      "name": "talk",
      "parameters": ["I will wait 2 seconds, then go to SpotA."],
      "runAfter": [],
      "delayBefore": 0
    }},
    {{
      "id": 1,
      "name": "moveToSpot",
      "parameters": ["SpotA"],
      "runAfter": [],
      "delayBefore": 2.0
    }},
    {{
      "id": 2,
      "name": "talk",
      "parameters": ["I am walking there now!"],
      "runAfter": [],
      "delayBefore": 2.0
    }},
    {{
      "id": 3,
      "name": "talk",
      "parameters": ["I arrived 1 second ago!"],
      "runAfter": [1],
      "delayBefore": 1.0
    }}
  ]
}}"""

    return [
        {"role": "system", "content": system},
        {"role": "user", "content": user},
    ]


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
                parameters=[str(p) for p in params],
                runAfter=[int(r) for r in run_after],
                delayBefore=float(item.get("delayBefore", 0.0))
            )
        )

    # Token counts live on the provider payload, not in the model's say/actions JSON
    return ActionsResponse(
        say=str(parsed.get("say") or ""),
        actions=actions,
        prompt_tokens=int(usage.get("prompt_tokens") or 0),
        completion_tokens=int(usage.get("completion_tokens") or 0),
    )