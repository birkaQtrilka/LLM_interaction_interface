import os
from pathlib import Path

import httpx
from dotenv import load_dotenv
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

load_dotenv(Path(__file__).resolve().parent / ".env")

PERSONA = "You are a scrub nurse in an operating room, reply in character, keep it short."

app = FastAPI()


class SendChatMessage(BaseModel):
    message: str


class GetMessage(BaseModel):
    say: str


def build_messages(user_text: str) -> list[dict]:
    return [
        {"role": "system", "content": PERSONA},
        {"role": "user", "content": user_text},
    ]

a
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
            "messages": build_messages(body.message),
        },
        timeout=60,
    )
    if not response.is_success:
        raise HTTPException(status_code=response.status_code, detail=response.text)

    say = response.json()["choices"][0]["message"]["content"]
    return GetMessage(say=say)
