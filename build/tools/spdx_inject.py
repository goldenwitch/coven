#!/usr/bin/env python3
"""
Add "SPDX-License-Identifier: BUSL-1.1" to the top of source files that lack it.

Usage:
  python build/tools/spdx_inject.py [path]

Notes:
  - Supports common comment syntaxes for: .c/.h/.cpp/.hpp/.cs/.java/.go/.js/.ts/.tsx/.rs,
    .py/.rb/.sh/.bash, .sql, .swift, .kt, .m/.mm.
  - Skips binary files and files in .git/, node_modules/, target/, dist/, build/ by default.
"""
import sys, os

HEADER_LINE = "SPDX-License-Identifier: BUSL-1.1"

LINE_COMMENT = {
    ".c": "//", ".h": "//", ".cpp": "//", ".hpp": "//",
    ".cs": "//", ".java": "//", ".go": "//", ".js": "//", ".ts": "//", ".tsx": "//",
    ".rs": "//", ".swift": "//", ".kt": "//", ".m": "//", ".mm": "//",
    ".sql": "--",
    ".py": "#", ".rb": "#", ".sh": "#", ".bash": "#",
}

SKIP_DIRS = {".git", "node_modules", "target", "dist", "build", ".venv", ".tox", "__pycache__"}

def should_skip(path: str) -> bool:
    parts = set(path.split(os.sep))
    return any(d in parts for d in SKIP_DIRS)

def process_file(path: str) -> None:
    ext = os.path.splitext(path)[1].lower()
    comment = LINE_COMMENT.get(ext)
    if not comment:
        return
    try:
        with open(path, "r", encoding="utf-8", errors="ignore") as f:
            content = f.read()
    except Exception:
        return
    if HEADER_LINE in content[:200]:  # already present near top
        return
    lines = content.splitlines()
    shebang = ""
    start = 0
    if lines and lines[0].startswith("#!"):
        shebang = lines[0]
        start = 1
    new_header = f"{comment} {HEADER_LINE}"
    new_content = "\n".join(([shebang] if shebang else []) + [new_header, ""] + lines[start:])
    if content and not content.endswith("\n"):
        new_content += "\n"
    try:
        with open(path, "w", encoding="utf-8") as f:
            f.write(new_content)
        print(f"Injected SPDX header: {path}")
    except Exception as e:
        print(f"Failed to write {path}: {e}")

def main() -> None:
    base = sys.argv[1] if len(sys.argv) > 1 else "."
    for root, dirs, files in os.walk(base):
        if should_skip(root):
            dirs[:] = []  # do not descend
            continue
        for name in files:
            process_file(os.path.join(root, name))

if __name__ == "__main__":
    main()

