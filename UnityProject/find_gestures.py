import argparse
import re
from collections import defaultdict
from pathlib import Path

DEFAULT_DIR = Path(
    r"C:\Unity\LLM_interaction_interface\UnityProject\Assets\Scripts\Gestures\Implementations"
)

# Matches: public string Layer => "hands";
# Also handles variations like { get; } = "hands"; or { get { return "hands"; } }
LAYER_PATTERN = re.compile(
    r'\bstring\s+Layer\s*(?:=>|\{\s*get;\s*\}\s*=|\{\s*get\s*=>|\{\s*get\s*\{\s*return)\s*"([^"]*)"'
)

NAME_PATTERN = re.compile(
    r'\bstring\s+Name\s*(?:=>|\{\s*get;\s*\}\s*=|\{\s*get\s*=>|\{\s*get\s*\{\s*return)\s*"([^"]*)"'
)


def strip_single_line_comments(content: str) -> str:
    return re.sub(r"//.*", "", content)


def scan_directory(target_dir: Path) -> None:
    if not target_dir.exists() or not target_dir.is_dir():
        print(f"Error: '{target_dir}' is not a valid directory.")
        return

    cs_files = sorted(target_dir.rglob("*.cs"))
    if not cs_files:
        print(f"No .cs files found in '{target_dir}'.")
        return

    layers_map: dict[str, list[Path]] = defaultdict(list)
    missing_layer_handlers: list[Path] = []

    print(f"Scanning '{target_dir}'...\n")

    for file_path in cs_files:
        content = file_path.read_text(encoding="utf-8-sig", errors="replace")
        cleaned = strip_single_line_comments(content)

        layer_match = LAYER_PATTERN.search(cleaned)
        name_match = NAME_PATTERN.search(cleaned)
        is_gesture_handler = "IGestureHandler" in cleaned

        rel_path = file_path.relative_to(target_dir)

        if layer_match:
            layer_name = layer_match.group(1)
            gesture_name = name_match.group(1) if name_match else "N/A"
            layers_map[layer_name].append(rel_path)
            print(f"[FOUND] {rel_path}")
            print(f"        Name : {gesture_name}")
            print(f"        Layer: {layer_name}\n")
        elif is_gesture_handler:
            missing_layer_handlers.append(rel_path)

    print("-" * 40)
    print("Summary of Layers Found:")
    if layers_map:
        for layer, files in sorted(layers_map.items()):
            print(f'  "{layer}": {len(files)} file(s)')
    else:
        print("  No layers found.")

    if missing_layer_handlers:
        print("\nWarning - IGestureHandler scripts missing a Layer property:")
        for path in missing_layer_handlers:
            print(f"  - {path}")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Check Layer names in Unity gesture C# scripts."
    )
    parser.add_argument(
        "directory",
        nargs="?",
        default=None,
        help="Directory to scan (defaults to this script's directory).",
    )
    args = parser.parse_args()

    target_dir = Path(args.directory).resolve() if args.directory else DEFAULT_DIR

    scan_directory(target_dir)


if __name__ == "__main__":
    main()