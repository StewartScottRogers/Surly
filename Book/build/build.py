#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Build the book from every recipe's recipe.yml and recipe.adoc.

For each recipe it writes, into the recipe's .generated/ folder: attributes.adoc
(metadata and verification status as AsciiDoc attributes), solution.adoc (the
solution commands and their expected output, as the book shows them),
claims.adoc (every other case as a checked claim), and beside the recipe run.sh
and run.ps1 (the solution, runnable by a reader on either platform). A recipe
whose solution is a C# program (schema 2, ADR-0008) shows the program's source
before the commands, and its run scripts call `dotnet run`. A recipe whose
solution is MCP calls shows each call, what it saves and what it must answer,
and gets mcp.json (a client configuration for both hosts) in place of the run
scripts. Then it
writes generated/ at the book root (identity, spine, index.json, by-protocol)
and renders out/surly.html and out/recipes/<key>.html with Asciidoctor.

Usage:  python3 build/build.py
Needs:  python3 with PyYAML; asciidoctor on PATH (npm install -g asciidoctor)
"""
import json, re, shlex, shutil, subprocess, sys
from pathlib import Path
import yaml
from recipes import (BOOK, recipes, book_values, server_commands, solution_servers, setup_commands, seed_commands, solution_case, case_command,
                     programs, versions_text, mcp_hosts)

GEN, OUT = BOOK / "generated", BOOK / "out"


def shell_block(lines, lang="shell"):
    return f"[source,{lang}]\n----\n" + "\n".join(lines) + "\n----\n"


def mcp_config(spec):
    """mcp.json: both hosts as an MCP client starts them, each confined by an environment
    variable (never a path argument, which cmd.exe breaks on a space). Each must be an absolute folder."""
    # A host build/packages.yml pins at a version is started from nuget.org, as a reader would
    # (ADR-0010): dnx <package>@<version> --yes. One not published there yet is a command on PATH.
    pins = yaml.safe_load((Path(__file__).resolve().parent / "packages.yml").read_text(encoding="utf-8")) or {}

    def host(command, package, root_var):
        version = pins.get(package)
        start = ({"command": "dnx", "args": [f"{package}@{version}", "--yes"]} if version not in (None, "*")
                 else {"command": command})
        return {**start, "env": {root_var: "/absolute/path/to/this/folder"}}

    servers = {}
    if "surl-mcp" in mcp_hosts(spec):
        servers["surl"] = host("surl-mcp", "Surly.Surl.Mcp", "SURL_MCP_ROOT")
    if "curl-mcp" in mcp_hosts(spec):
        servers["curl"] = host("curl-mcp", "Surly.Curl.Mcp", "CURL_MCP_ROOT")
    return json.dumps({"mcpServers": servers}, indent=2) + "\n"


def expected_lines(case):
    exp = case["expect"]
    out = []
    if "stdout" in exp:
        out += exp["stdout"].rstrip("\n").split("\n")
    elif "stdout-matches" in exp:
        out.append(f"(output matching /{exp['stdout-matches']}/)")
    return out


URL = re.compile(r"[a-z][a-z0-9+.-]*://\S+")


def listen_lines(args):
    """What one surl command line prints as it starts, as measured on Surl v0.1.10: a Listening
    line per listen URL in order, then one per --proxy URL, then "Control endpoint listening on"
    for --control (its URL with a trailing slash). --user-ldap's URL is a directory surl queries,
    not one it listens on."""
    listen, proxy, control = [], [], []
    skip = False
    for i, arg in enumerate(args):
        if skip:
            skip = False
            continue
        name, eq, value = arg.partition("=")
        if name in ("--control", "--proxy", "--user-ldap"):
            if not eq:
                value = args[i + 1] if i + 1 < len(args) else ""
                skip = True
            if name == "--control" and URL.fullmatch(value):
                control.append(value if re.search(r"://[^/]+/", value) else value + "/")
            elif name == "--proxy" and URL.fullmatch(value):
                proxy.append(value)
            continue
        if URL.fullmatch(arg):
            listen.append(arg)
    return ([f"Listening on {u}" for u in listen + proxy]
            + [f"Control endpoint listening on {u}" for u in control])


def source_block(recipe, program):
    """A C# program's source, as the book shows it."""
    return (f".{program}\n[source,csharp]\n----\n"
            + (recipe / program).read_text(encoding="utf-8").rstrip("\n") + "\n----\n")


def mono(text):
    """Literal monospace for a table cell: no replacements (=> stays =>), no attribute
    references, and an escaped | does not end the cell. `+...+` cannot hold a +, so text
    with one goes through pass:c[...], where only ] needs escaping."""
    text = text.replace("|", r"\|")
    if "+" not in text:
        return "`+" + text + "+`"
    return "`pass:c[" + text.replace("]", r"\]") + "]`"


def speaker(case):
    """Who prints the solution's output, in the sentence before it."""
    if "program" in case:
        return "the program"
    return f"`{case['run'].split()[0]}`"


def fixture_files(spec):
    """Every file a recipe's run needs, in order: the recipe's fixtures, then the solution case's own."""
    files = dict(spec.get("fixtures") or {})
    files.update(solution_case(spec).get("fixtures") or {})
    return files


def sh_quote(text):
    return "'" + text.replace("'", "'\\''") + "'"


def fixture_lines_sh(files, shown=()):
    """Shell lines that make the recipe's files, so nothing a command uses appears unannounced:
    mkdir for the folders (a .keep placeholder is only its folder), printf for a one-line file, a
    quoted heredoc for a longer one. A file in `shown` (the prose includes it in full) is named,
    not repeated."""
    folders, lines = [], []
    for path in files:
        parent = path.rsplit("/", 1)[0] if "/" in path else ""
        if parent and parent not in folders:
            folders.append(parent)
    if folders:
        lines.append("mkdir -p " + " ".join(shlex.quote(f) for f in folders))
    for path, content in files.items():
        if path.endswith("/.keep") or path == ".keep":
            continue
        target = shlex.quote(path)
        if path in shown:
            lines.append(f"# save {path}, shown in this recipe, here")
        elif content == "":
            lines.append(f": > {target}")
        elif content.endswith("\r\n") and "\r" not in content.replace("\r\n", ""):
            # Every line ends CRLF (mail): the lines as they read, and awk adds each CR as it writes.
            body = content[:-2].split("\r\n")
            end = "EOF"
            while end in body:
                end += "_"
            lines += [f"awk '{{printf \"%s\\r\\n\", $0}}' > {target} <<'{end}'"] + body + [end]
        elif any(ch == "\r" or (ord(ch) < 32 and ch not in "\n\t") for ch in content):
            # Another mix of line ends, or bytes a shell string cannot hold (STOMP's NUL): printf's
            # own escapes say each one, in octal.
            fmt = "".join("\\\\" if ch == "\\" else "%%" if ch == "%" else "\\n" if ch == "\n"
                          else f"\\{ord(ch):03o}" if ord(ch) < 32 and ch != "\t" else ch for ch in content)
            lines.append(f"printf {sh_quote(fmt)} > {target}")
        elif not content.endswith("\n"):
            lines.append(f"printf '%s' {sh_quote(content)} > {target}")
        elif content.count("\n") == 1:
            lines.append(f"printf '%s\\n' {sh_quote(content[:-1])} > {target}")
        else:
            body = content[:-1].split("\n")
            end = "EOF"
            while end in body:
                end += "_"
            lines += [f"cat > {target} <<'{end}'"] + body + [end]
    return lines


def ps_string(text):
    """A PowerShell double-quoted string holding text exactly, line ends included, whatever the
    line endings of the .ps1 file that holds it."""
    for a, b in (("`", "``"), ("$", "`$"), ('"', '`"'), ("\r", "`r"), ("\n", "`n"), ("\t", "`t"), ("\0", "`0")):
        text = text.replace(a, b)
    text = "".join(f"$([char]{ord(ch)})" if ord(ch) < 32 else ch for ch in text)
    return f'"{text}"'


def fixture_lines_ps(files):
    """The same files for run.ps1, written byte for byte (no BOM, no CRLF) with WriteAllText."""
    lines = []
    folders = []
    for path in files:
        parent = path.rsplit("/", 1)[0] if "/" in path else ""
        if parent and parent not in folders:
            folders.append(parent)
    if folders:
        lines.append("New-Item -ItemType Directory -Force -Path " + ", ".join("'" + f.replace("'", "''") + "'" for f in folders) + " | Out-Null")
    for path, content in files.items():
        if path.endswith("/.keep") or path == ".keep":
            continue
        lines.append(f"[IO.File]::WriteAllText((Join-Path $PWD '{path.replace(chr(39), chr(39) * 2)}'), {ps_string(content)})")
    return lines


def write_recipe_files(recipe, spec):
    gen = recipe / ".generated"
    gen.mkdir(exist_ok=True)
    # The fixtures, on disk, so a reader can run run.sh from the folder as checked out.
    for rel_path, content in (spec.get("fixtures") or {}).items():
        f = recipe / rel_path
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_text(content, encoding="utf-8", newline="")   # exactly as written: no CRLF on Windows
    values = book_values(spec)
    sol = solution_case(spec)
    servers = solution_servers(spec, values)   # only those the solution talks to
    v = spec["verified"]

    # attributes.adoc: everything the prose or the renderer might want to show.
    attrs = [f":recipe-key: {spec['key']}", f":recipe-status: {v.get('status', 'draft')}",
             f":recipe-tier: {spec['tier']}", f":recipe-protocols: {', '.join(spec['protocols'])}"]
    attrs.append(f":recipe-surface: {spec['surface']}")
    if v.get("status") == "verified":
        attrs.append(f":recipe-verified: {versions_text(v)}, {v['date']}, {', '.join(v['platforms'])}")
    (gen / "attributes.adoc").write_text("// Generated by build.py from recipe.yml. Do not edit.\n"
                                         + "\n".join(attrs) + "\n", encoding="utf-8")

    # solution.adoc: the commands as the book shows them, then what they print.
    sol_cmd = case_command(sol, values)
    setup = setup_commands(spec, values)
    seed = seed_commands(spec, values)
    # Every file the commands use is made in front of the reader (no file appears unannounced):
    # a file the prose already shows in full is named instead of repeated.
    files = fixture_files(spec)
    adoc = (recipe / "recipe.adoc").read_text(encoding="utf-8")
    shown = {n for n in files if f"include::{n}[" in adoc}
    cmds = fixture_lines_sh(files, shown) + setup + [s + " &" for s in servers] + seed + [sol_cmd]
    # surl prints one Listening line per listen URL on its command line, in order.
    # A listen URL is a whole argument; a URL inside an option's value (--fault "...;listener=...") is not.
    listening = [line for cmd in servers for line in listen_lines(shlex.split(cmd))]
    printed = listening + expected_lines(sol)
    status_note = ""
    if v.get("status") == "verified":
        status_note = (f"\n[.verified]\n_Verified against {versions_text(v)} on "
                       f"{', '.join(v['platforms'])}, {v['date']}._\n")
    elif v.get("status") == "broken":
        status_note = f"\nWARNING: This recipe failed verification on {v['date']} against {versions_text(v)}; see verified.yml.\n"
    elif spec["surface"] == "csharp":
        status_note = "\nNOTE: Draft: not yet verified against released packages.\n"
    elif spec["surface"] == "mcp":
        status_note = "\nNOTE: Draft: not yet verified against a released surl-mcp and curl-mcp.\n"
    else:
        status_note = "\nNOTE: Draft: not yet verified against a released surl and curl.\n"
    program = source_block(recipe, sol["program"]) + "\n" if "program" in sol else ""
    who = "a second `surl`" if speaker(sol) == "`surl`" else speaker(sol)
    quiet = not any(expected_lines(sol))
    if servers:
        said = (f"`surl` prints that it is listening; {who} prints nothing and exits {sol['expect'].get('exit', 0)}:"
                if quiet else f"`surl` prints that it is listening, then {who} prints what it got:")
    else:
        said = (f"{who[0].upper()}{who[1:]} prints nothing and exits {sol['expect'].get('exit', 0)}." if quiet
                else f"{who[0].upper()}{who[1:]} prints:")
    (gen / "solution.adoc").write_text(
        "// Generated by build.py from recipe.yml. Do not edit.\n" + program + shell_block(cmds)
        + f"\n{said}\n\n" + (shell_block(printed, "text") if printed else "") + status_note, encoding="utf-8")

    # claims.adoc: every non-solution case, as a checked claim with its command and result.
    rows = []
    for c in spec["cases"]:
        if c is sol:
            continue
        mark = {"pass": "✔", "fail": "✘", "skipped": "–"}.get((v.get("cases") or {}).get(c["id"]), "·")
        result = f"exit {c['expect'].get('exit', 0)}"
        if any(expected_lines(c)):
            result += ", prints " + mono(" ".join(expected_lines(c)))
        elif c["expect"].get("stdout") == "":
            result += ", prints nothing"
        command = mono(case_command(c, values))
        if c.get("platforms"):
            names = {"windows": "Windows", "linux": "Linux", "macos": "macOS"}
            command = command + " (" + " and ".join(names[p] for p in c["platforms"]) + " only)"
        if c.get("restart"):
            command = "after restarting " + ", ".join(f"`{n}`" for n in c["restart"]) + ": " + command
        rows.append(f"| {mark} | {str(c['claim']).replace('|', chr(92) + '|')} | {command} | {result}")
    claims = ("// Generated by build.py from recipe.yml. Do not edit.\n"
              ".Checked claims\n[cols=\"1,6,6,3\",options=\"header\"]\n|===\n| | Claim | Command | Result\n"
              + "\n".join(rows) + "\n|===\n") if rows else "// No checked claims beyond the solution.\n"
    # The servers the claims use that the Solution has not shown (a default server beside a
    # loosened one, for instance), so a reader can run every claim as printed.
    others = [s for s in server_commands(spec, values) if s not in servers]
    if others and rows:
        claims += "\nThe claims also use these servers, started the same way:\n\n" + shell_block([s + " &" for s in others])
    # The source of every program a claim runs that the Solution has not already shown.
    for prog in programs(spec):
        if prog != sol.get("program"):
            claims += "\n" + source_block(recipe, prog)
    (gen / "claims.adoc").write_text(claims, encoding="utf-8")

    # run.sh and run.ps1: the solution, for a reader to run.
    head = f"# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.\n# Generated by build.py from recipe.yml for recipe {spec['key']}. Do not edit.\n"
    sh = ["#!/usr/bin/env sh", head.rstrip(), "set -e", 'cd "$(dirname "$0")"'] + fixture_lines_sh(files) + setup
    for s in servers:
        sh.append(s + " &")
    # A solution that is meant to fail must not end the script before the server is stopped.
    fails = "expect" in sol and sol["expect"].get("exit", 0) != 0
    sh += (["sleep 1"] if servers else []) + seed + [sol_cmd + (" || true" if fails else "")] + (["kill $(jobs -p)"] if servers else [])
    (recipe / "run.sh").write_text("\n".join(sh) + "\n", encoding="utf-8")
    (recipe / "run.sh").chmod(0o755)
    # Setup lines run through cmd, so a redirect writes bytes, not Windows PowerShell 5.1's UTF-16.
    ps = [head.rstrip(), "Set-Location $PSScriptRoot"] + fixture_lines_ps(files) + [
        "cmd /c '" + c.replace("'", "''") + "'" for c in setup_commands(spec, {**values, "null": "NUL"})]
    for i, s in enumerate(servers):
        exe, *args = s.replace("{{null}}", "NUL").split()
        ps.append(f"$server{i} = Start-Process {exe} -ArgumentList {', '.join(repr(a) for a in args)} -PassThru -NoNewWindow")
    ps_cmd = case_command(sol, {**values, "null": "NUL"})
    if ps_cmd.startswith("curl "):
        ps_cmd = "curl.exe " + ps_cmd[len("curl "):]
    ps += [c.replace("curl ", "curl.exe ", 1) if c.startswith("curl ") else c
           for c in seed_commands(spec, {**values, "null": "NUL"})] if not servers else []
    if servers:
        ps.append("Start-Sleep 1")
        ps += [c.replace("curl ", "curl.exe ", 1) if c.startswith("curl ") else c
               for c in seed_commands(spec, {**values, "null": "NUL"})]
        ps.append("try { " + ps_cmd + " } finally { "
                  + "; ".join(f"Stop-Process $server{i}" for i in range(len(servers))) + " }")
    else:
        ps.append(ps_cmd)
    (recipe / "run.ps1").write_text("\n".join(ps) + "\n", encoding="utf-8")
    if spec["surface"] == "mcp":
        # mcp.json: the two hosts, for a reader's own MCP client; run.sh runs the C# agent.
        (recipe / "mcp.json").write_text(mcp_config(spec), encoding="utf-8")


def problem_sentence(recipe):
    text = (recipe / "recipe.adoc").read_text(encoding="utf-8")
    m = re.search(r"==== Problem\s+(.+?)\n\n", text, re.S)
    return " ".join(m.group(1).split()) if m else ""


def run(cmd):
    # Resolve the program through PATH and PATHEXT: npm installs asciidoctor as a .cmd on
    # Windows, which a bare name does not reach.
    exe = shutil.which(cmd[0])
    if not exe:
        sys.exit(f"{cmd[0]} not found on PATH (npm install -g asciidoctor)")
    r = subprocess.run([exe] + cmd[1:], capture_output=True, text=True)
    if r.returncode:
        sys.exit(r.stderr or r.stdout)
    if r.stderr.strip():
        print(r.stderr.strip(), file=sys.stderr)


def version_attributes():
    """AsciiDoc attributes for the versions a reader installs (ADR-0010), so the preface's install
    commands follow every re-pin: {surly-curl-version} and the like from packages.yml, one per
    package pinned at a version, and {surl-release} (the GitHub release tag) from pins.yml."""
    here = Path(__file__).resolve().parent
    pins = yaml.safe_load((here / "packages.yml").read_text(encoding="utf-8")) or {}
    lines = [f":{name.lower().replace('.', '-')}-version: {v}" for name, v in pins.items() if v not in (None, "*")]
    release = re.search(r"^surl:\s*[0-9a-f]{40}\s*#\s*Surl\s+(v[0-9][^\s,]*)", (here / "pins.yml").read_text(encoding="utf-8"), re.M)
    if release:
        lines.append(f":surl-release: {release.group(1)}")
    return "".join(line + "\n" for line in lines)


def main():
    GEN.mkdir(exist_ok=True)
    (OUT / "recipes").mkdir(parents=True, exist_ok=True)
    ident = yaml.safe_load((BOOK / "book.yml").read_text(encoding="utf-8"))
    (GEN / "identity.adoc").write_text(
        f"= {ident['title']}: {ident['subtitle']}\n{ident['author']}\n"
        f":publisher: {ident['publisher']}\n:edition: {ident['edition']}\n"
        f":copyright: {ident['copyright']}\n:revnumber: {ident['edition']}\n:revdate: {ident['year']}\n"
        + version_attributes(), encoding="utf-8")

    spine, index, by_proto = [], [], {}
    seen_part = seen_chapter = None
    for part, chapter, recipe, spec in recipes():
        if part != seen_part:
            spine.append(f"include::../{part.relative_to(BOOK).as_posix()}/part.adoc[]\n\n")
            seen_part = part
        if chapter != seen_chapter:
            spine.append(f"include::../{chapter.relative_to(BOOK).as_posix()}/chapter.adoc[]\n\n")
            seen_chapter = chapter
        write_recipe_files(recipe, spec)
        rel = (recipe / "recipe.adoc").relative_to(BOOK).as_posix()
        spine.append(f"include::../{rel}[]\n\n")
        v = spec["verified"]
        index.append({
            "key": spec["key"], "title": spec["title"], "problem": problem_sentence(recipe),
            "protocols": spec["protocols"], "tier": spec["tier"], "surface": spec["surface"],
            "surl-options": spec.get("surl-options", []), "curl-options": spec.get("curl-options", []),
            "api-types": spec.get("api-types", []),
            "programs": [f"{recipe.relative_to(BOOK).as_posix()}/{p}" for p in programs(spec)],
            "exit-codes": spec.get("exit-codes", []), "references": spec.get("references", []),
            "claims": [c["claim"] for c in spec["cases"]],
            "status": v.get("status", "draft"), "verified": {k: v[k] for k in ("versions", "date", "platforms") if k in v},
            "part": part.name, "chapter": chapter.name,
            "source": rel, "spec": f"{recipe.relative_to(BOOK).as_posix()}/recipe.yml",
            "run": f"{recipe.relative_to(BOOK).as_posix()}/{'mcp.json' if 'mcp' in solution_case(spec) else 'run.sh'}",
            "mcp-tools": spec.get("mcp-tools", []),
            "rendered": f"out/recipes/{spec['key']}.html"})
        for p in spec["protocols"]:
            by_proto.setdefault(p, []).append(spec["key"])
        single = GEN / f"recipe-{spec['key']}.adoc"
        single.write_text("include::identity.adoc[]\n:doctype: article\n:xrefstyle: short\n:icons: font\n"
                          ":source-highlighter: highlight.js\n:experimental:\n\n"
                          f"include::../{rel}[leveloffset=-1]\n\ninclude::../references/bibliography.adoc[]\n",
                          encoding="utf-8")
        run(["asciidoctor", "-o", str(OUT / "recipes" / f"{spec['key']}.html"), str(single)])

    (GEN / "spine.adoc").write_text("".join(spine), encoding="utf-8")
    (GEN / "index.json").write_text(json.dumps({"book": ident, "recipes": index}, indent=2), encoding="utf-8")
    lines = ["// Generated. Do not edit.\n[appendix]\n== Recipes by Protocol\n"]
    for p in sorted(by_proto):
        lines.append(f"\n=== {p}\n\n" + "".join(f"* <<{k}>>\n" for k in by_proto[p]))
    (GEN / "by-protocol.adoc").write_text("".join(lines), encoding="utf-8")
    run(["asciidoctor", "-o", str(OUT / "surly.html"), str(BOOK / "book.adoc")])
    print(f"{len(index)} recipe(s); book at {OUT / 'surly.html'}; index at {GEN / 'index.json'}")


if __name__ == "__main__":
    main()
