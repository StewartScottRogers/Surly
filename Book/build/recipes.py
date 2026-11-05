#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Shared knowledge of what a recipe is: where they live, how recipe.yml is
read, how placeholders are substituted, and what the generated files are.
Used by build.py, verify.py and lint.py so the three cannot disagree."""
import re, sys
from pathlib import Path
import yaml

BOOK = Path(__file__).resolve().parent.parent
BOOK_PORT = "8080"                      # the port the book shows; verify allocates a real one
PLACEHOLDER = re.compile(r"\{\{([a-z-]+)\}\}")
REQUIRED = ["schema", "key", "title", "protocols", "tier", "cases"]
TIERS = ["foundations", "working-servers", "secure-servers", "advanced", "new-protocols"]


def recipes():
    """Yield (part_dir, chapter_dir, recipe_dir, spec) in book order."""
    for part in sorted((BOOK / "parts").iterdir()):
        if not (part / "part.adoc").exists():
            continue
        for chapter in sorted((part / "chapters").iterdir()):
            if not (chapter / "chapter.adoc").exists():
                continue
            for recipe in sorted((chapter / "recipes").iterdir()):
                if (recipe / "recipe.yml").exists():
                    yield part, chapter, recipe, load_spec(recipe)


def load_spec(recipe_dir):
    spec = yaml.safe_load((recipe_dir / "recipe.yml").read_text(encoding="utf-8"))
    missing = [k for k in REQUIRED if k not in spec]
    if missing:
        sys.exit(f"{recipe_dir / 'recipe.yml'}: missing {missing}")
    if spec["key"] != recipe_dir.name:
        sys.exit(f"{recipe_dir / 'recipe.yml'}: key '{spec['key']}' must equal the folder name '{recipe_dir.name}'")
    if spec["tier"] not in TIERS:
        sys.exit(f"{recipe_dir / 'recipe.yml'}: tier must be one of {TIERS}")
    solutions = [c for c in spec["cases"] if c.get("role") == "solution"]
    if len(solutions) != 1:
        sys.exit(f"{recipe_dir / 'recipe.yml'}: exactly one case must have role: solution")
    for c in spec["cases"]:
        for k in ("id", "claim", "run", "expect"):
            if k not in c:
                sys.exit(f"{recipe_dir / 'recipe.yml'}: case {c.get('id', '?')} lacks '{k}'")
    spec["verified"] = load_verified(recipe_dir)
    return spec


def load_verified(recipe_dir):
    p = recipe_dir / "verified.yml"
    return yaml.safe_load(p.read_text(encoding="utf-8")) if p.exists() else {"status": "draft"}


def substitute(text, values):
    """Replace {{name}} placeholders; %{http_code} and other single braces are untouched."""
    def one(m):
        name = m.group(1)
        if name not in values:
            sys.exit(f"unknown placeholder {{{{{name}}}}} in: {text}")
        return str(values[name])
    return PLACEHOLDER.sub(one, text)


def book_values():
    """Placeholder values as the book shows them (POSIX form)."""
    return {"port": BOOK_PORT, "null": "/dev/null"}


def windows_values(port):
    return {"port": port, "null": "NUL"}


def posix_values(port):
    return {"port": port, "null": "/dev/null"}


def server_commands(spec, values):
    return [substitute(s["command"], values) for s in (spec.get("servers") or {}).values()]


def solution_case(spec):
    return next(c for c in spec["cases"] if c.get("role") == "solution")
