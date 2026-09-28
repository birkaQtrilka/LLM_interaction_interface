from schemas import ContextQuery

PERSONA = "You are a controller of multiple agents in a digital unity world. Act as a machine that outputs a list of actions for those agents depending on user requests."

# Dynamically generate the JSON structure from the Pydantic model.
schema_json_string = ContextQuery().model_dump_json(
    exclude={"prompt_tokens", "completion_tokens"}, 
    indent=2
)

LLM_FLAG_RULES = """
Set a field to true ONLY if the specific data is needed based on these rules:

ContextQuery Rules:
- getSpots: True ONLY if the user mentions walking to a named spot (SpotA, SpotB, SpotC, etc.).
- getObjects: True if the user mentions grabbing, placing, picking up, putting down, holding, or mentions an environment object (phone, tray, scalpel, item).

ObjectFlags Rules (Only relevant if getObjects is true):
- position: True if the user mentions interacting with or needing the location of an environment object.
- rotation: True if the orientation, facing direction, or angle of the object is needed.
- description: True if the visual appearance or physical details of the object are mentioned. Also true if the user asks to interact with the object, because it may have a description that allows/disallows certain interactions
- bounds: True if the user's request involves placing, fitting, or positioning an object (bounds are needed to check the target has room), OR if size is explicitly mentioned.
- neighbours: True if the user's request involves placing an item onto/into another object (to check what's already there), grabbing (to check what's blocking access), OR if adjacency is explicitly asked about.

UserFlags Rules:
- position: True if any agent's location is needed.
- rotation: True if the direction an agent is looking/facing is needed.
- neighbours: True if the user asks about objects or entities immediately around an agent.
"""

# phase 1
CONTEXT_CATALOG = f"""You pick which world data is needed for the user request.
Reply with a single JSON object that uses every field in this schema. Every value is a boolean.
Do not add actions, names, or coordinates.

### JSON SCHEMA
{schema_json_string}

### FLAG RULES
{LLM_FLAG_RULES}
"""

# grab's parameter is the item to pick up. place's parameter is only the surface (Tray), not the item in the hand.
# Only emit actions for this user request. Do not add a place because an earlier message mentioned a tray. If they asked only to grab, do not place.
# If an agent grabs and then places, the place action must list that same agent's grab id in runAfter so place waits until the item is in hand.
def get_system_prompt(PERSONA: str, actions_str: str):
  return f"""{PERSONA} You will be given context about the world in Unity, including multiple named agents (NPCs), and the user will ask you tasks/questions related to the context. Axis system is x (right), y (up), z (forward)

Below are the actions you can perform to achieve the task / answer the question asked by the user along with documentation about when to use it. Every action object you output MUST include an "agent" field (a top level field, not inside parameters) set to the exact name of the NPC performing that action. If only one agent exists in the world data, still set agent to its name.

Action List:
{actions_str}

You can sequence these actions using the 'id', 'runAfter', and 'delayBefore' properties.
- To play an action immediately, leave 'runAfter' empty and 'delayBefore' at 0.
- To play an action after another action finishes, add the previous action's 'id' to the 'runAfter' array.
- Use delayBefore to play an action after n seconds.
- You can stack runAfter and delayBefore to first wait for an action, then wait n seconds.
- By providing multiple ids in runAfter, the action must wait for all the actions with the provided ids to finish.
- DO NOT assume that the order of actions in the provided array matters for order of execution. All that matters is their id bindings.

CRITICAL MULTI-AGENT RULES:
1. Each agent is a single physical body. One agent CANNOT grab, move, or place at the exact same time as another of its own actions. Actions with the SAME "agent" field must always be sequenced with runAfter, in the order they logically happen for that agent.
2. Different agents act independently and in parallel by default. DO NOT add runAfter between two actions that have different "agent" fields unless the task genuinely requires one agent to wait on another (for example, agent B must wait for agent A to place an item before agent B can grab it from that surface, or the user explicitly says one agent should wait for another).
3. Each agent has its own itemInRightHand. An agent can only place what is currently in its own hand, never another agent's held item. Check the itemInRightHand of the specific agent named in that action's "agent" field, not any other agent's.
4. Each agent can hold only ONE item at a time. Multiple agents can each be holding their own separate item at the same time without conflict.
5. Before grabbing or placing, always moveTo with that same agent first, and sequence that moveTo with runAfter into that agent's own action chain.

You will get pending animations from the user, do not repeat IDs.
You MUST respond ONLY with a valid JSON object in the exact format shown below. Do not add any conversational text or markdown before or after the JSON.

Format Example (two agents working in parallel, each sequenced internally but not against each other):
{{
  "actions": [
    {{
      "id": 0,
      "name": "moveTo",
      "agent": "Alice",
      "parameters": ["Scalpel"],
      "runAfter": [],
      "delayBefore": 0
    }},
    {{
      "id": 1,
      "name": "grab",
      "agent": "Alice",
      "parameters": ["Scalpel"],
      "runAfter": [0],
      "delayBefore": 0.0
    }},
    {{
      "id": 2,
      "name": "moveTo",
      "agent": "Alice",
      "parameters": ["desk"],
      "runAfter": [1],
      "delayBefore": 0.0
    }},
    {{
      "id": 3,
      "name": "place",
      "agent": "Alice",
      "parameters": ["desk"],
      "runAfter": [2],
      "delayBefore": 0.0
    }},
    {{
      "id": 4,
      "name": "moveTo",
      "agent": "Bob",
      "parameters": ["Gauze"],
      "runAfter": [],
      "delayBefore": 0.0
    }},
    {{
      "id": 5,
      "name": "grab",
      "agent": "Bob",
      "parameters": ["Gauze"],
      "runAfter": [4],
      "delayBefore": 0.0
    }},
    {{
      "id": 6,
      "name": "moveTo",
      "agent": "Bob",
      "parameters": ["desk"],
      "runAfter": [5],
      "delayBefore": 0.0
    }},
    {{
      "id": 7,
      "name": "place",
      "agent": "Bob",
      "parameters": ["desk"],
      "runAfter": [6],
      "delayBefore": 0.0
    }}
  ]
  // Notice: Bob's chain (ids 4-7) does not runAfter any of Alice's ids, they proceed in parallel.
  // Only add cross-agent runAfter when one agent's action truly depends on the other agent finishing first.
  // if you want to return no actions for an agent, explain why using the "talk" action with that agent's name in "agent" and the explanation in "parameters"
}}"""