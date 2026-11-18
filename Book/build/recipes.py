#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Shared knowledge of what a recipe is: where they live, how recipe.yml is
read, how placeholders are substituted, how a case's command is spelled, and
what the generated files are. Used by build.py, verify.py and lint.py so the
three cannot disagree.

Schema 1 cases are shell lines (run:). Schema 2 (ADR-0008) adds program:, a C#
file-based app beside recipe.yml run with `dotnet run`, and reserves mcp:."""
import re, sys
from pathlib import Path
import yaml

BOOK = Path(__file__).resolve().parent.parent
BOOK_PORT = "8080"                      # the port the book shows; verify allocates a real one
PLACEHOLDER = re.compile(r"\{\{([a-z-]+)\}\}")
REQUIRED = ["schema", "key", "title", "protocols", "tier", "cases"]
TIERS = ["foundations", "working-servers", "secure-servers", "advanced", "new-protocols"]
SCHEMAS = [1, 2]
SURFACES = ["console", "csharp", "mcp"]
CASE_KINDS = ["run", "program", "mcp"]
PACKAGE_LINE = re.compile(r"^#:package\s+([A-Za-z0-9_.-]+)(?:@(\S+))?\s*$", re.M)
PROJECT_LINE = re.compile(r"^#:project\s+(\S+)", re.M)
PACKAGES_FILE = Path(__file__).resolve().parent / "packages.yml"


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
    where = recipe_dir / "recipe.yml"
    if spec["schema"] not in SCHEMAS:
        sys.exit(f"{where}: schema must be one of {SCHEMAS}")
    if spec["tier"] not in TIERS:
        sys.exit(f"{where}: tier must be one of {TIERS}")
    spec.setdefault("surface", "console")
    if spec["surface"] not in SURFACES:
        sys.exit(f"{where}: surface must be one of {SURFACES}")
    if spec["surface"] != "console" and spec["schema"] < 2:
        sys.exit(f"{where}: surface {spec['surface']} needs schema: 2")
    solutions = [c for c in spec["cases"] if c.get("role") == "solution"]
    if len(solutions) != 1:
        sys.exit(f"{where}: exactly one case must have role: solution")
    for c in spec["cases"]:
        for k in ("id", "claim", "expect"):
            if k not in c:
                sys.exit(f"{where}: case {c.get('id', '?')} lacks '{k}'")
        kinds = [k for k in CASE_KINDS if k in c]
        if len(kinds) != 1:
            sys.exit(f"{where}: case {c['id']} must have exactly one of {CASE_KINDS}, has {kinds or 'none'}")
        if kinds[0] != "run" and spec["schema"] < 2:
            sys.exit(f"{where}: case {c['id']} uses {kinds[0]}:, which needs schema: 2")
        if kinds[0] == "mcp":
            sys.exit(f"{where}: case {c['id']}: mcp: cases are decided (ADR-0008 item 7) but not built yet")
        if kinds[0] == "program":
            program = c["program"]
            if not isinstance(program, str) or not program.endswith(".cs") or Path(program).is_absolute() or ".." in Path(program).parts:
                sys.exit(f"{where}: case {c['id']}: program must be a .cs file inside the recipe folder")
            if not (recipe_dir / program).is_file():
                sys.exit(f"{where}: case {c['id']}: program {program} does not exist")
            if not isinstance(c.get("args", []), list):
                sys.exit(f"{where}: case {c['id']}: args must be a list")
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


def quote(arg):
    """Quote one argument for display and for both shells, only when it needs it."""
    return arg if re.fullmatch(r"[A-Za-z0-9_@%+=:,./{}-]+", arg) else '"' + arg.replace('"', '\\"') + '"'


def case_command(case, values):
    """The command a case runs, as one line: the shell line, or `dotnet run` for a program."""
    if "run" in case:
        return substitute(case["run"], values)
    args = [quote(substitute(a, values)) for a in case.get("args", [])]
    return " ".join(["dotnet", "run", case["program"]] + (["--"] + args if args else []))


def programs(spec):
    """The distinct C# program files a recipe's cases run, in case order."""
    return list(dict.fromkeys(c["program"] for c in spec["cases"] if "program" in c))


def program_packages(path):
    """{package: version or None} for every #:package line in a C# file-based app."""
    return {m.group(1): m.group(2) for m in PACKAGE_LINE.finditer(Path(path).read_text(encoding="utf-8"))}


def program_projects(path):
    """Every #:project line, which the book does not allow (ADR-0008 item 5)."""
    return PROJECT_LINE.findall(Path(path).read_text(encoding="utf-8"))


def pinned_packages():
    """{package: version or None}: the one place package versions are pinned (ADR-0008 item 6)."""
    return yaml.safe_load(PACKAGES_FILE.read_text(encoding="utf-8")) or {}


def versions_text(v):
    """'surl 1.0, curl 1.0, dotnet 11.0.100, Surl.Api 0.1.0' from a verified.yml record."""
    return ", ".join(f"{k} {ver}" for k, ver in (v.get("versions") or {}).items())
