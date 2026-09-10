import os
from pathlib import Path

import httpx
from dotenv import load_dotenv

load_dotenv(Path(__file__).resolve().parent / ".env")

my_file_content = Path("unityRequestPrompt.txt").read_text(encoding="utf-8")

response = httpx.post(
    "https://api.openai.com/v1/chat/completions",
    headers={"Authorization": f"Bearer {os.environ['OPENAI_API_KEY']}"},
    json={
        "model": "gpt-4o-mini",
        "messages": [{"role": "user", "content": my_file_content}],
    },
)
print(response.json()["choices"][0]["message"]["content"])