import os
from pathlib import Path

import httpx
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

load_dotenv(Path(__file__).resolve().parent / ".env")

PERSONA = "You are a scrub nurse in an operating room, reply in character, keep it short."

app = FastAPI()


class TurnRequest(BaseModel):
    message: str


class TurnResponse(BaseModel):
    say: str


@app.post("/v1/turn", response_model=TurnResponse)
def turn(body: TurnRequest) -> TurnResponse:
    api_key = os.environ.get("OPENAI_API_KEY")
    if not api_key:
        raise HTTPException(status_code=500, detail="OPENAI_API_KEY is not set")

    response = httpx.post(
        "https://api.openai.com/v1/chat/completions",
        headers={"Authorization": f"Bearer {api_key}"},
        json={
            "model": "gpt-4o-mini",
            "messages": [
                {"role": "system", "content": PERSONA},
                {"role": "user", "content": body.message},
            ],
        },
        timeout=60,
    )
    if not response.is_success:
        raise HTTPException(status_code=response.status_code, detail=response.text)

    say = response.json()["choices"][0]["message"]["content"]
    return TurnResponse(say=say)
