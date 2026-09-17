from pydantic import BaseModel, Field

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