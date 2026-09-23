from schemas import ContextQuery

PERSONA = "You are a controller of an agent in a digital unity world. Act as a machine that outputs a list of actions for that agent depending on user requests."

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
- description: True if the visual appearance or physical details of the object are mentioned. Also true if the user asks to interact with the object, because it may have a description that disallows certain interactions
- bounds: True if the user's request involves placing, fitting, or positioning an object (bounds are needed to check the target has room), OR if size is explicitly mentioned.
- neighbours: True if the user's request involves placing an item onto/into another object (to check what's already there), grabbing (to check what's blocking access), OR if adjacency is explicitly asked about.

UserFlags Rules:
- position: True if the user's location is needed.
- rotation: True if the direction the user is looking/facing is needed.
- neighbours: True if the user asks about objects or entities immediately around themselves.
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
# If you grab and then place, the place action must list the grab id in runAfter so place waits until the item is in hand.
def get_system_prompt(PERSONA: str, actions_str: str):
  return f"""{PERSONA} You will be given context about the world in Unity and the user will ask you tasks/questions related to the context. Axis system is x (right), y (up), z (forward)
Below are the actions you can perform to achieve the task / answer the question asked by the user along with documentation about when to use it. 

Action List:
{actions_str}

You can sequence these actions using the 'id', 'runAfter', and 'delayBefore' properties.
- To play an action immediately, leave 'runAfter' empty and 'delayBefore' at 0.
- To play an action after another action finishes, add the previous action's 'id' to the 'runAfter' array.
- Use delayBefore to play an action after n seconds.
- You can stack runAfter and delayBefore to first wait for an action, then wait n seconds.
- By providing multiple ids in runAfter, the action must wait for all the actions with the provided ids to finish.
- DO NOT assume that the order of actions in the provided array matters for order of execution. All that matters is their id bindings.

CRITICAL SEQUENCING RULES:
1. Physical limitations: You are a single person. You CANNOT grab or move to multiple distinct items at the exact same time. 
2. Chaining sequences: When moving or interacting with multiple items (e.g., grabbing item A, then grabbing item B), you MUST sequence them. The action to grab the second item MUST have the 'id' of the previous 'place' or 'drop' action in its 'runAfter' array.

You will get pending animations from the user, do not repeat IDs.
You MUST respond ONLY with a valid JSON object in the exact format shown below. Do not add any conversational text or markdown before or after the JSON.
Format Example (Notice how fetching the second item waits for the first item to be placed):
{{
  "actions": [
    {{
      "id": 0,
      "name": "grab",
      "parameters": ["Scalpel"],
      "runAfter": [],
      "delayBefore": 0
    }},
    {{
      "id": 1,
      "name": "place",
      "parameters": [ "desk" ],
      "runAfter": [0],
      "delayBefore": 0.0
    }},
    {{
      "id": 2,
      "name": "grab",
      "parameters": ["Gauze"],
      "runAfter": [1], 
      "delayBefore": 0.0
    }},
    {{
      "id": 3,
      "name": "place",
      "parameters": [ "desk" ],
      "runAfter": [2],
      "delayBefore": 0.0
    }}
  ]
  // I skipped moving for shorter example but DON'T FORGET to move to the object before you grab it. Always move to object when grabbing/placing 
  // if you want to return no actions, explain why using the "talk" action
}}"""
