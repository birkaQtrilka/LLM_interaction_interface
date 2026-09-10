import os
from pathlib import Path

import httpx
from dotenv import load_dotenv

load_dotenv(Path(__file__).resolve().parent / ".env")

messages = []
headers = {"Authorization": f"Bearer {os.environ['OPENAI_API_KEY']}"}

print("Type a message. Empty line, quit, or exit to stop.")

while True:
    user_text = input("you> ").strip()
    if not user_text or user_text.lower() in {"quit", "exit"}:
        break

    messages.append({"role": "user", "content": user_text})

    response = httpx.post(
        "https://api.openai.com/v1/chat/completions",
        headers=headers,
        json={
            "model": "gpt-4o-mini",
            "messages": messages,
        },
        timeout=60,
    )
    data = response.json()
    if not response.is_success:
        print(response.text)
        messages.pop()
        continue

    llm_text = data["choices"][0]["message"]["content"]
    print("\n" + llm_text + "\n")
    print("Total tokens: ", data["usage"]["total_tokens"], "\n")
    messages.append({"role": "assistant", "content": llm_text})
