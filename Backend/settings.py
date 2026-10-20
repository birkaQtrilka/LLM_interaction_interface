import os
from pathlib import Path

from dotenv import load_dotenv

BASE_DIR = Path(__file__).resolve().parent
load_dotenv(BASE_DIR / ".env")

# Override in Backend/.env with OPENAI_MODEL=...
OPENAI_MODEL = os.environ.get("OPENAI_MODEL", "gpt-6-luna").strip() or "gpt-6-luna"
