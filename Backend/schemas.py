from pydantic import BaseModel, Field

class ActionsRequestBody(BaseModel):
    message: str
    # Unity sends a snapshot object; curl can still send a string
    world: str | dict = ""
    # Empty skips the session log, so curl still works before Unity sends a session.
    session_id: str = ""
    log_directory: str = ""
    description: str = ""

class ContextRequestBody(BaseModel):
    message: str
    session_id: str = ""
    log_directory: str = ""
    description: str = ""

class SessionRequestBody(BaseModel):
    session_id: str
    log_directory: str = ""
    description: str = ""

class FeedbackRequestBody(BaseModel):
    session_id: str
    natural: int
    natural_note: str = ""
    accurate: int
    accurate_note: str = ""
    log_directory: str = ""
    description: str = ""

class ErrorRequestBody(BaseModel):
    session_id: str
    message: str
    stack: str = ""
    log_directory: str = ""
    description: str = ""

class UserFlags(BaseModel):
    position: bool = False
    rotation: bool = False
    neighbours: bool = False

class ObjectFlags(BaseModel):
    position: bool = False
    rotation: bool = False
    description: bool = False
    bounds: bool = False
    neighbours: bool = False

class ContextQuery(BaseModel):
    getSpots: bool = False
    getObjects: bool = False
    objectFlags: ObjectFlags = Field(default_factory=ObjectFlags)
    userFlags: UserFlags = Field(default_factory=UserFlags)
    # purely for logging, won't be used in game logic
    prompt_tokens: int = 0
    completion_tokens: int = 0

class ActionData(BaseModel):
    id: int = 0
    name: str
    agent: str
    parameters: list[str] = Field(default_factory=list)
    runAfter: list[int] = Field(default_factory=list)
    delayBefore: float = 0.0


class ActionsResponse(BaseModel):
    actions: list[ActionData] = Field(default_factory=list)
    prompt_tokens: int = 0
    completion_tokens: int = 0