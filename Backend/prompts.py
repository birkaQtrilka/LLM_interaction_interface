from schemas import ContextQuery

PERSONA = "You are a scrub nurse in an operating room, reply in character, keep it short."

# Dynamically generate the JSON structure from the Pydantic model.
schema_json_string = ContextQuery().model_dump_json(
    exclude={"prompt_tokens", "completion_tokens"}, 
    indent=2
)

# Round-1 catalog: flags only, no coordinates and no action names
CONTEXT_CATALOG = f"""You pick which world data is needed for the user request.
Reply with a single JSON object that uses every field in this schema. Every value is a boolean.
Do not add actions, names, or coordinates.
{schema_json_string}
Set a field true only if that data is needed.
"""

def get_system_prompt(PERSONA: str, actions_str: str):
  return f"""{PERSONA} You will be given context about the world and the user will ask you tasks/questions related to the context.
Below are the actions you can perform to achieve the task / answer the question asked by the user along with documentation about when to use it. 

Action List:
{actions_str}

You can sequence these actions using the 'id', 'runAfter', and 'delayBefore' properties.
- To play an action immediately, leave 'runAfter' empty and 'delayBefore' at 0.
- To play actions at the same time, give them the same 'runAfter' array and the same 'delayBefore'.
- To play an action after another action finishes, add the previous action's 'id' to the 'runAfter' array.
- To play an action after another action finishes + n seconds, use 'runAfter' with the previous action's 'id' and set 'delayBefore' to n.
- To play an action after n seconds from the start, leave 'runAfter' empty and set 'delayBefore' to n.

You will get pending animations from the user, do not repeat IDs.
You MUST respond ONLY with a valid JSON object in the exact format shown below. Do not add any conversational text or markdown before or after the JSON.

Format:
{{
  "actions": [
    {{
      "id": 0,
      "name": "talk",
      "parameters": ["I will wait 2 seconds, then go to SpotA."],
      "runAfter": [],
      "delayBefore": 0
    }},
    {{
      "id": 1,
      "name": "moveToSpot",
      "parameters": ["SpotA"],
      "runAfter": [],
      "delayBefore": 2.0
    }},
    {{
      "id": 2,
      "name": "talk",
      "parameters": ["I am walking there now!"],
      "runAfter": [],
      "delayBefore": 2.0
    }},
    {{
      "id": 3,
      "name": "talk",
      "parameters": ["I arrived 1 second ago!"],
      "runAfter": [1],
      "delayBefore": 1.0
    }}
  ]
}}"""
