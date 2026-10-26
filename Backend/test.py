import os

import httpx

from settings import OPENAI_MODEL

response = httpx.post(
    "https://api.openai.com/v1/chat/completions",
    headers={"Authorization": f"Bearer {os.environ['OPENAI_API_KEY']}"},
    json={
        "model": OPENAI_MODEL,
        "messages": [{"role": "user", "content": "Reply with exactly: pass"}],
    },
)
print(response.json()["choices"][0]["message"]["content"])