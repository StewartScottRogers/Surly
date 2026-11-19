#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Shared knowledge of what a recipe is: where they live, how recipe.yml is
read, how placeholders are substituted, how a case's command is spelled, and
what the generated files are. Used by build.py, verify.py and lint.py so the
three cannot disagree.

Schema 1 cases are shell lines (run:). Schema 2 (ADR-0008) adds program:, a C#
file-based app beside recipe.yml run with `dotnet run`, and mcp:, a list of tool
calls to surl-mcp and curl-mcp."""
import json, re, sys
from pathlib import Path
import yaml

BOOK = Path(__file__).resolve().parent.parent
BOOK_PORT = "8080"                      # the port the book shows; verify allocates a real one
# {{port}} and {{port2}} to {{port9}}: one free port each, shown in the book as 8080 to 8088.
PORT_NAMES = ["port"] + [f"port{i}" for i in range(2, 10)]
PLACEHOLDER = re.compile(r"\{\{([a-z0-9-]+)\}\}")
REQUIRED = ["schema", "key", "title", "protocols", "tier", "cases"]
TIERS = ["foundations", "working-servers", "secure-servers", "advanced", "new-protocols"]
SCHEMAS = [1, 2]
SURFACES = ["console", "csharp", "mcp"]
CASE_KINDS = ["run", "program", "mcp"]
PACKAGE_LINE = re.compile(r"^#:package\s+([A-Za-z0-9_.-]+)(?:@(\S+))?\s*$", re.M)
PROJECT_LINE = re.compile(r"^#:project\s+(\S+)", re.M)
PACKAGES_FILE = Path(__file__).resolve().parent / "packages.yml"
MCP_HOSTS = {"surl": "surl-mcp", "curl": "curl-mcp"}      # host: in a call -> the program verify starts
MCP_CALL_KEYS = {"host", "tool", "arguments", "save", "expect"}
MCP_EXPECT_KEYS = {"result-contains", "is-error", "error-contains"}


def recipes(only=None):
    """only: a set of keys; folders with other names are skipped before their recipe.yml
    is read, so a half-written recipe elsewhere cannot stop a run of these."""
    """Yield (part_dir, chapter_dir, recipe_dir, spec) in book order."""
    for part in sorted((BOOK / "parts").iterdir()):
        if not (part / "part.adoc").exists():
            continue
        for chapter in sorted((part / "chapters").iterdir()):
            if not (chapter / "chapter.adoc").exists():
                continue
            for recipe in sorted((chapter / "recipes").iterdir()):
                if (recipe / "recipe.yml").exists() and (not only or recipe.name in only):
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
        for k in ("id", "claim"):
            if k not in c:
                sys.exit(f"{where}: case {c.get('id', '?')} lacks '{k}'")
        kinds = [k for k in CASE_KINDS if k in c]
        if len(kinds) != 1:
            sys.exit(f"{where}: case {c['id']} must have exactly one of {CASE_KINDS}, has {kinds or 'none'}")
        if kinds[0] != "run" and spec["schema"] < 2:
            sys.exit(f"{where}: case {c['id']} uses {kinds[0]}:, which needs schema: 2")
        if kinds[0] == "mcp":
            check_mcp_case(where, c)
        elif "expect" not in c:
            sys.exit(f"{where}: case {c['id']} lacks 'expect'")
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


def check_mcp_case(where, case):
    """An mcp: case is a list of calls; each call's own expect: says what it must answer."""
    if "expect" in case:
        sys.exit(f"{where}: case {case['id']}: an mcp: case puts expect: on each call, not on the case")
    calls = case["mcp"]
    if not isinstance(calls, list) or not calls:
        sys.exit(f"{where}: case {case['id']}: mcp: must be a list of one or more calls")
    for i, call in enumerate(calls, 1):
        at = f"{where}: case {case['id']}, call {i}"
        if not isinstance(call, dict) or not {"host", "tool"} <= call.keys():
            sys.exit(f"{at}: needs host: and tool:")
        if call.keys() - MCP_CALL_KEYS:
            sys.exit(f"{at}: unknown key(s) {sorted(call.keys() - MCP_CALL_KEYS)}")
        if call["host"] not in MCP_HOSTS:
            sys.exit(f"{at}: host must be one of {list(MCP_HOSTS)}")
        if not isinstance(call.get("arguments", {}), dict):
            sys.exit(f"{at}: arguments must be a mapping")
        for name in (call.get("save") or {}):
            if not re.fullmatch(r"[a-z-]+", name):
                sys.exit(f"{at}: saved name '{name}' must be lower-case words joined by hyphens")
        expect = call.get("expect") or {}
        if expect.keys() - MCP_EXPECT_KEYS:
            sys.exit(f"{at}: unknown expect key(s) {sorted(expect.keys() - MCP_EXPECT_KEYS)}")


def load_verified(recipe_dir):
    p = recipe_dir / "verified.yml"
    return yaml.safe_load(p.read_text(encoding="utf-8")) if p.exists() else {"status": "draft"}


def substitute_deep(value, values):
    """substitute() through every string in a structure of mappings and lists."""
    if isinstance(value, str):
        whole = PLACEHOLDER.fullmatch(value)
        if whole and whole.group(1) in values and not isinstance(values[whole.group(1)], str):
            return values[whole.group(1)]          # "{{seq}}" alone keeps a saved number's type
        return substitute(value, values)
    if isinstance(value, list):
        return [substitute_deep(v, values) for v in value]
    if isinstance(value, dict):
        return {k: substitute_deep(v, values) for k, v in value.items()}
    return value


def substitute(text, values):
    """Replace {{name}} placeholders; %{http_code} and other single braces are untouched."""
    def one(m):
        name = m.group(1)
        if name not in values:
            sys.exit(f"unknown placeholder {{{{{name}}}}} in: {text}")
        return str(values[name])
    return PLACEHOLDER.sub(one, text)


def book_values(spec=None):
    """Placeholder values as the book shows them (POSIX form). A name an MCP call
    saves is shown as itself, {{name}}, since its value is known only at run time."""
    values = {name: str(int(BOOK_PORT) + i) for i, name in enumerate(PORT_NAMES)}
    values["null"] = "/dev/null"
    if spec:
        values.update({n: "{{" + n + "}}" for n in saved_names(spec)})
    return values


def windows_values(ports):
    """ports: {placeholder name: free port number}, one for each of PORT_NAMES."""
    return {**ports, "null": "NUL"}


def posix_values(ports):
    return {**ports, "null": "/dev/null"}


def solution_servers(spec, values):
    """The server commands the solution needs: those sharing a port placeholder with the
    solution's command, or every server when the solution names no port (a program, MCP)."""
    sol = solution_case(spec)
    text = sol.get("run", "") + " " + " ".join(map(str, sol.get("args", [])))
    used = {n for n in PORT_NAMES if "{{" + n + "}}" in text}
    chosen = [s["command"] for s in (spec.get("servers") or {}).values()
              if not used or any("{{" + n + "}}" in s["command"] for n in used)]
    return [substitute(c, values) for c in chosen]


def server_commands(spec, values):
    return [substitute(s["command"], values) for s in (spec.get("servers") or {}).values()]


def solution_case(spec):
    return next(c for c in spec["cases"] if c.get("role") == "solution")


def quote(arg):
    """Quote one argument for display and for both shells, only when it needs it."""
    return arg if re.fullmatch(r"[A-Za-z0-9_@%+=:,./{}-]+", arg) else '"' + arg.replace('"', '\\"') + '"'


def saved_names(spec):
    """Every name an mcp: call saves, which later calls use as {{name}}."""
    return [n for c in spec["cases"] for call in c.get("mcp", []) for n in (call.get("save") or {})]


def mcp_hosts(spec):
    """The MCP hosts a recipe's cases call, as surl-mcp and curl-mcp."""
    return sorted({MCP_HOSTS[call["host"]] for c in spec["cases"] for call in c.get("mcp", [])})


def mcp_tools_used(spec):
    return list(dict.fromkeys(call["tool"] for c in spec["cases"] for call in c.get("mcp", [])))


def call_line(call, values):
    """One call as the book shows it: surl-mcp start_server {"listen_urls": [...]}."""
    args = json.dumps(substitute_deep(call.get("arguments") or {}, values), ensure_ascii=False)
    return f"{MCP_HOSTS[call['host']]} {call['tool']} {args}"


def case_command(case, values):
    """The command a case runs, as one line: the shell line, `dotnet run` for a program,
    or its MCP calls separated by semicolons."""
    if "run" in case:
        return substitute(case["run"], values)
    if "mcp" in case:
        return "; ".join(call_line(call, values) for call in case["mcp"])
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
