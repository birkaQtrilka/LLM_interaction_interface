import json
import os
import time
import uuid
from datetime import datetime
from pathlib import Path

import httpx
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException

from schemas import (
    ActionsRequestBody, ContextRequestBody, ContextQuery, 
    ActionsResponse, ActionData, ObjectFlags, UserFlags, SessionRequestBody,
    FeedbackRequestBody, ErrorRequestBody,
)
from prompts import PERSONA, CONTEXT_CATALOG, get_system_prompt
BASE_DIR = Path(__file__).resolve().parent
load_dotenv(BASE_DIR / ".env")

with open(BASE_DIR / "actions.json", "r") as f:
    ACTIONS = json.load(f)

app = FastAPI()
LOGS_DIR = BASE_DIR / "logs"
# The system text is the same on every turn, so the log omits it unless this is on
LOG_SYSTEM = False

def log_dir(directory: str) -> Path:
    # Empty keeps Backend/logs, a relative name is created inside it, and a full path is used as given
    text = (directory or "").strip()
    if not text:
        return LOGS_DIR
    raw = Path(text)
    if raw.is_absolute():
        return raw
    candidate = (LOGS_DIR / raw).resolve()
    root = LOGS_DIR.resolve()
    if candidate != root and root not in candidate.parents:
        raise HTTPException(status_code=400, detail="A relative log directory must stay inside Backend/logs")
    return candidate

def session_path(session_id: str, directory: str = "") -> Path:
    # Filename is the GUID, so anything else could escape the logs folder
    try:
        parsed = uuid.UUID(session_id)
    except ValueError:
        raise HTTPException(status_code=400, detail="session_id must be a GUID")
    return log_dir(directory) / f"{parsed}.json"

def new_session(path: Path, description: str = "") -> dict:
    data = {
        "session_id": path.stem,
        "started_at": datetime.now().isoformat(timespec="seconds"),
        "ended_at": None,
        "turns": [],
    }
    apply_description(data, description)
    return data

def apply_description(data: dict, description: str) -> None:
    text = (description or "").strip()
    if text:
        data["description"] = text

def write_session(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2))

def elapsed_s(started: float) -> float:
    # Seconds since the request arrived, sampled when the model call returns
    return round(time.perf_counter() - started, 3)

def append_turn(session_id: str, endpoint: str, system: str, user: str, response: object, llm_s: float, directory: str = "", description: str = "") -> None:
    if not session_id:
        return
    path = session_path(session_id, directory)
    if path.exists():
        data = json.loads(path.read_text())
    else:
        # Start may not have arrived yet, so create the file and keep this turn
        data = new_session(path, description)
    apply_description(data, description)
    turn = {
        "at": datetime.now().isoformat(timespec="seconds"),
        "endpoint": endpoint,
        "user": user,
        "response": response,
        "llm_s": llm_s,
    }
    if LOG_SYSTEM:
        turn["system"] = system
    data["turns"].append(turn)
    write_session(path, data)

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




def build_messages(user_text: str, world: str, addressee: str = "") -> list[dict]:
    user = f"Context:\n{world}\n\nUser request: {user_text}"
        
    action_lines = []
    for action in ACTIONS:
        action_lines.append(f"// {action['doc']}\n{action['name']}({action['args']})")
    
    actions_str = "\n".join(action_lines)
    print("------------- SYSTEM -------------------")

    system = get_system_prompt(PERSONA, actions_str, addressee)
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
    started = time.perf_counter()
    if body.session_id:
        session_path(body.session_id, body.log_directory)
    try:
        parsed, usage = openai_json(
            [
                {"role": "system", "content": CONTEXT_CATALOG},
                {"role": "user", "content": body.message},
            ]
        )
    except HTTPException as exc:
        append_turn(body.session_id, "context", CONTEXT_CATALOG, body.message, exc.detail, elapsed_s(started), body.log_directory, body.description)
        raise
    llm_s = elapsed_s(started)
    user = flags_from(parsed.get("userFlags"))
    objects = flags_from(parsed.get("objectFlags"))
    reply = ContextQuery(
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
    append_turn(body.session_id, "context", CONTEXT_CATALOG, body.message, reply.model_dump(), llm_s, body.log_directory, body.description)
    return reply


@app.post("/v1/turn", response_model=ActionsResponse)
def turn(body: ActionsRequestBody) -> ActionsResponse:
    started = time.perf_counter()
    if body.session_id:
        session_path(body.session_id, body.log_directory)
    messages = build_messages(body.message, body.world, body.addressee)
    system = messages[0]["content"]
    user = messages[1]["content"]
    try:
        parsed, usage = openai_json(messages)
    except HTTPException as exc:
        append_turn(body.session_id, "turn", system, user, exc.detail, elapsed_s(started), body.log_directory, body.description)
        raise
    llm_s = elapsed_s(started)

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

    reply = ActionsResponse(
        actions=actions,
        prompt_tokens=int(usage.get("prompt_tokens") or 0),
        completion_tokens=int(usage.get("completion_tokens") or 0),
    )
    append_turn(body.session_id, "turn", system, user, reply.model_dump(), llm_s, body.log_directory, body.description)
    return reply


@app.post("/v1/session/start")
def session_start(body: SessionRequestBody) -> dict:
    path = session_path(body.session_id, body.log_directory)
    # A turn can arrive before this call. Do not wipe turns already written.
    if path.exists():
        data = json.loads(path.read_text())
    else:
        data = new_session(path, body.description)
    apply_description(data, body.description)
    write_session(path, data)
    return {"session_id": path.stem}


@app.post("/v1/session/error")
def session_error(body: ErrorRequestBody) -> dict:
    path = session_path(body.session_id, body.log_directory)
    if path.exists():
        data = json.loads(path.read_text())
    else:
        # Start may not have arrived yet, so create the file and keep this error
        data = new_session(path, body.description)
    apply_description(data, body.description)
    errors = data.setdefault("errors", [])
    errors.append({
        "at": datetime.now().isoformat(timespec="seconds"),
        "message": body.message,
        "stack": body.stack,
    })
    write_session(path, data)
    return {"session_id": path.stem}


@app.post("/v1/session/feedback")
def session_feedback(body: FeedbackRequestBody) -> dict:
    if body.natural < 1 or body.natural > 5 or body.accurate < 1 or body.accurate > 5:
        raise HTTPException(status_code=400, detail="Scores must be from 1 to 5")
    path = session_path(body.session_id, body.log_directory)
    if not path.exists():
        raise HTTPException(status_code=404, detail="Session log was not found")
    data = json.loads(path.read_text())
    data["feedback"] = {
        "natural": body.natural,
        "natural_note": body.natural_note,
        "accurate": body.accurate,
        "accurate_note": body.accurate_note,
    }
    write_session(path, data)
    return {"session_id": path.stem}


@app.post("/v1/session/end")
def session_end(body: SessionRequestBody) -> dict:
    path = session_path(body.session_id, body.log_directory)
    if not path.exists():
        return {"session_id": path.stem}
    data = json.loads(path.read_text())
    data["ended_at"] = datetime.now().isoformat(timespec="seconds")
    write_session(path, data)
    return {"session_id": path.stem}