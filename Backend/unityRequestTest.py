import os
from pathlib import Path

import httpx

from settings import OPENAI_MODEL

my_file_content = Path("unityRequestPrompt.txt").read_text(encoding="utf-8")

response = httpx.post(
    "https://api.openai.com/v1/chat/completions",
    headers={"Authorization": f"Bearer {os.environ['OPENAI_API_KEY']}"},
    json={
        "model": OPENAI_MODEL,
        "messages": [{"role": "user", "content": my_file_content}],
    },
)
print(response.json()["choices"][0]["message"]["content"])
