import json
from pathlib import Path

# What the fields in scenario/*.json mean (JSON cannot hold comments):
#
# characters.json, a list of NPCs. name must match the NPC object name in Unity.
#   in_room_at_start  false means the NPC is outside until called in (the Surgeon, #212)
#   start_spot        name of a walk spot in the Unity scene
#   responsible_for, behaviour  the role, not a script of what to do next
#
# checkpoints.json
#   case         background that is true for the whole scenario
#   checkpoints  states of the case, in order. A state says what is true now, not what to do
#     vitals         what the monitor shows. null means the outline gives no value
#     tom            how the patient responds. The model writes his actual lines from this
#     findings       what an examination finds. Only revealed when someone examines that thing
#     team           mood of a character in this state, keyed by character name
#     advances_when  the event that moves the case to the next state. Judged by the model (#210),
#                    and kept out of the NPC prompt so NPCs do not hint at it

CHARACTER_FIELDS = ["name", "role", "in_room_at_start", "start_spot", "responsible_for", "behaviour"]
CASE_FIELDS = ["player_role", "patient", "handover", "task_distribution"]
CHECKPOINT_FIELDS = ["id", "name", "patient", "vitals", "tom", "findings", "team", "advances_when"]


def is_text(value) -> bool:
    return isinstance(value, str) and value.strip() != ""


def check_characters(characters, problems: list[str]) -> set[str]:
    if not isinstance(characters, list) or not characters:
        problems.append("characters.json must be a non-empty list")
        return set()

    names = set()
    for i, c in enumerate(characters):
        where = f"characters[{i}]"
        if not isinstance(c, dict):
            problems.append(f"{where} must be an object")
            continue
        for field in CHARACTER_FIELDS:
            if field not in c:
                problems.append(f"{where} is missing '{field}'")
        if not isinstance(c.get("in_room_at_start", False), bool):
            problems.append(f"{where}.in_room_at_start must be true or false")
        for field in CHARACTER_FIELDS:
            if field != "in_room_at_start" and field in c and not is_text(c[field]):
                problems.append(f"{where}.{field} must be a non-empty string")

        name = c.get("name")
        if is_text(name):
            # Gesture tags drop names with spaces, so agent names are single words
            if " " in name:
                problems.append(f"{where}.name '{name}' must not contain spaces")
            if name in names:
                problems.append(f"{where}.name '{name}' is used twice")
            names.add(name)
    return names


def check_checkpoints(data, names: set[str], problems: list[str]):
    if not isinstance(data, dict):
        problems.append("checkpoints.json must be an object with 'case' and 'checkpoints'")
        return

    case = data.get("case")
    if not isinstance(case, dict):
        problems.append("checkpoints.json is missing the 'case' object")
    else:
        for field in CASE_FIELDS:
            if not is_text(case.get(field)):
                problems.append(f"case.{field} must be a non-empty string")

    checkpoints = data.get("checkpoints")
    if not isinstance(checkpoints, list) or not checkpoints:
        problems.append("checkpoints must be a non-empty list")
        return

    for i, cp in enumerate(checkpoints):
        where = f"checkpoints[{i}]"
        if not isinstance(cp, dict):
            problems.append(f"{where} must be an object")
            continue
        for field in CHECKPOINT_FIELDS:
            if field not in cp:
                problems.append(f"{where} is missing '{field}'")

        # ids run 1, 2, 3... so the order in the file is the order of the case
        if cp.get("id") != i + 1:
            problems.append(f"{where}.id must be {i + 1}, got {cp.get('id')!r}")
        for field in ["name", "patient", "tom", "advances_when"]:
            if field in cp and not is_text(cp[field]):
                problems.append(f"{where}.{field} must be a non-empty string")

        vitals = cp.get("vitals", {})
        if not isinstance(vitals, dict):
            problems.append(f"{where}.vitals must be an object")
        else:
            for key, value in vitals.items():
                # bool is a subclass of int in Python, so it is ruled out on its own
                if isinstance(value, bool) or not (value is None or isinstance(value, (int, float, str))):
                    problems.append(f"{where}.vitals.{key} must be a number, a string or null")

        findings = cp.get("findings", {})
        if not isinstance(findings, dict) or not all(is_text(v) for v in findings.values()):
            problems.append(f"{where}.findings must be an object of non-empty strings")

        team = cp.get("team", {})
        if not isinstance(team, dict):
            problems.append(f"{where}.team must be an object")
        else:
            for who, mood in team.items():
                if who not in names:
                    problems.append(f"{where}.team has '{who}', which is not in characters.json")
                if not is_text(mood):
                    problems.append(f"{where}.team.{who} must be a non-empty string")


def load_scenario(folder: Path) -> dict:
    """Reads characters.json and checkpoints.json. Raises ValueError listing every problem."""
    with open(folder / "characters.json", "r", encoding="utf-8") as f:
        characters = json.load(f)
    with open(folder / "checkpoints.json", "r", encoding="utf-8") as f:
        checkpoints = json.load(f)

    problems: list[str] = []
    names = check_characters(characters, problems)
    check_checkpoints(checkpoints, names, problems)
    if problems:
        raise ValueError(f"Scenario in {folder} is invalid:\n- " + "\n- ".join(problems))

    return {"characters": characters, "case": checkpoints["case"], "checkpoints": checkpoints["checkpoints"]}


# Run "python scenario_loader.py" to check the files without starting the server
if __name__ == "__main__":
    scenario = load_scenario(Path(__file__).resolve().parent / "scenario")
    print(f"OK: {len(scenario['characters'])} characters, {len(scenario['checkpoints'])} checkpoints")
    for cp in scenario["checkpoints"]:
        print(f"  {cp['id']}. {cp['name']}")
