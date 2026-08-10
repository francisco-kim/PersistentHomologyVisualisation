#!/usr/bin/env python3
"""Fail on a stylesheet a browser would silently take apart.

CSS has no parse errors in the sense a compiler means: a browser meeting
something it cannot read discards declarations until it finds its footing
again, paints the rest, and says nothing. An unterminated comment cost this
project a rule that had been written, reviewed and committed - the page simply
rendered as though it were not there.

The two faults below are the ones that swallow whole rules, and neither is
visible when reading a diff. This is not a linter and is not trying to be one:
a real one would want a Node toolchain for a project that has a single
stylesheet.
"""

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent


def check(path):
    """Return a list of complaints about one stylesheet."""
    text = path.read_text(encoding="utf-8")
    faults = []

    # Comment nesting. CSS comments do not nest, so a "/*" inside a comment is
    # very nearly always a "*/" that was meant to close it and was mistyped or
    # dropped - the fault that motivated this script.
    depth = 0
    line = 1
    index = 0
    opened_at = None

    while index < len(text):
        if text.startswith("/*", index):
            if depth:
                faults.append(f"{path}:{line}: '/*' inside a comment opened at line {opened_at}")
            else:
                opened_at = line
            depth += 1
            index += 2
            continue

        if text.startswith("*/", index):
            if depth:
                depth -= 1
            else:
                faults.append(f"{path}:{line}: '*/' with no comment open")
            index += 2
            continue

        if text[index] == "\n":
            line += 1
        index += 1

    if depth:
        faults.append(f"{path}:{opened_at}: comment is never closed")

    # Brace balance, ignoring anything commented out. A stray brace runs one
    # rule into the next and takes both down with it.
    stripped = []
    depth = 0
    index = 0
    while index < len(text):
        if text.startswith("/*", index):
            depth += 1
            index += 2
        elif text.startswith("*/", index):
            depth = max(0, depth - 1)
            index += 2
        else:
            if not depth:
                stripped.append(text[index])
            index += 1

    code = "".join(stripped)
    opens, closes = code.count("{"), code.count("}")
    if opens != closes:
        faults.append(f"{path}: {opens} '{{' against {closes} '}}'")

    return faults


def main():
    sheets = sorted(REPO_ROOT.glob("src/**/wwwroot/**/*.css"))
    sheets = [s for s in sheets if "/lib/" not in s.as_posix()]

    if not sheets:
        print("No stylesheets found - check the glob in this script.", file=sys.stderr)
        return 1

    faults = [fault for sheet in sheets for fault in check(sheet)]

    for fault in faults:
        print(fault, file=sys.stderr)

    if faults:
        print(f"\n{len(faults)} problem(s) in {len(sheets)} stylesheet(s).", file=sys.stderr)
        return 1

    print(f"{len(sheets)} stylesheet(s) OK.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
