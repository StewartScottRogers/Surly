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
import json, re, shutil, subprocess, sys
from pathlib import Path
import yaml
from recipes import (BOOK, recipes, book_values, server_commands, solution_case, case_command,
                     programs, versions_text, call_line, mcp_hosts, substitute_deep)

GEN, OUT = BOOK / "generated", BOOK / "out"


def shell_block(lines, lang="shell"):
    return f"[source,{lang}]\n----\n" + "\n".join(lines) + "\n----\n"


def mcp_listing(case, values):
    """An mcp: case as the book shows it: each call, what it saves, what it must answer."""
    lines = []
    for call in case["mcp"]:
        lines.append(call_line(call, values))
        for name, path in (call.get("save") or {}).items():
            lines.append(f"  saves {path} as {{{{{name}}}}}")
        exp = call.get("expect") or {}
        if exp.get("is-error"):
            lines.append("  answers with an error" + (f" containing {exp['error-contains']!r}" if "error-contains" in exp else ""))
        if "result-contains" in exp:
            lines.append("  answers " + json.dumps(substitute_deep(exp["result-contains"], values), ensure_ascii=False))
    return lines


def mcp_result(case, values):
    """The Result column for an mcp: claim: what each checked call must answer."""
    said = []
    for call in case["mcp"]:
        exp = call.get("expect") or {}
        if exp.get("is-error"):
            said.append(f"`{call['tool']}` is an error" + (f" containing {mono(exp['error-contains'])}" if "error-contains" in exp else ""))
        if "result-contains" in exp:
            said.append(f"`{call['tool']}` answers " + mono(json.dumps(substitute_deep(exp['result-contains'], values), ensure_ascii=False)))
    return "; ".join(said) or "every call succeeds"


def mcp_config(spec):
    """mcp.json: both hosts as an MCP client starts them. CURL_MCP_ROOT must be an absolute folder."""
    servers = {}
    if "surl-mcp" in mcp_hosts(spec):
        servers["surl"] = {"command": "surl-mcp", "args": ["--root", "."]}
    if "curl-mcp" in mcp_hosts(spec):
        servers["curl"] = {"command": "curl-mcp", "env": {"CURL_MCP_ROOT": "/absolute/path/to/this/folder"}}
    return json.dumps({"mcpServers": servers}, indent=2) + "\n"


def expected_lines(case):
    exp = case["expect"]
    out = []
    if "stdout" in exp:
        out += exp["stdout"].rstrip("\n").split("\n")
    elif "stdout-matches" in exp:
        out.append(f"(output matching /{exp['stdout-matches']}/)")
    return out


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
    servers = server_commands(spec, values)
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
    if "mcp" in sol:
        listing = mcp_listing(sol, values)
        said = "In order: each call to `surl-mcp` or `curl-mcp`, what it saves for later calls, and what it must answer."
    sol_cmd = None if "mcp" in sol else case_command(sol, values)
    cmds = [s + " &" for s in servers] + [sol_cmd]
    # surl prints one Listening line per listen URL on its command line, in order.
    listening = [f"Listening on {u}" for cmd in servers for u in re.findall(r"(?<![\w/])[a-z][a-z0-9+.-]*://\S+", cmd)]
    printed = listening + ([] if "mcp" in sol else expected_lines(sol))
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
    if "mcp" in sol:
        (gen / "solution.adoc").write_text(
            "// Generated by build.py from recipe.yml. Do not edit.\n" + f"{said}\n\n"
            + shell_block(listing, "text") + status_note, encoding="utf-8")
    else:
        program = source_block(recipe, sol["program"]) + "\n" if "program" in sol else ""
        said = (f"`surl` prints that it is listening, then {'a second `surl`' if speaker(sol) == '`surl`' else speaker(sol)} prints what it got:" if servers
                else f"{speaker(sol)[0].upper()}{speaker(sol)[1:]} prints:")
        (gen / "solution.adoc").write_text(
            "// Generated by build.py from recipe.yml. Do not edit.\n" + program + shell_block(cmds)
            + f"\n{said}\n\n" + shell_block(printed, "text") + status_note, encoding="utf-8")

    # claims.adoc: every non-solution case, as a checked claim with its command and result.
    rows = []
    for c in spec["cases"]:
        if c is sol:
            continue
        mark = {"pass": "✔", "fail": "✘"}.get((v.get("cases") or {}).get(c["id"]), "·")
        if "mcp" in c:
            result = mcp_result(c, values)
        else:
            result = f"exit {c['expect'].get('exit', 0)}"
            if any(expected_lines(c)):
                result += ", prints " + mono(" ".join(expected_lines(c)))
            elif c["expect"].get("stdout") == "":
                result += ", prints nothing"
        rows.append(f"| {mark} | {c['claim']} | {mono(case_command(c, values))} | {result}")
    claims = ("// Generated by build.py from recipe.yml. Do not edit.\n"
              ".Checked claims\n[cols=\"1,6,6,3\",options=\"header\"]\n|===\n| | Claim | Command | Result\n"
              + "\n".join(rows) + "\n|===\n") if rows else "// No checked claims beyond the solution.\n"
    # The source of every program a claim runs that the Solution has not already shown.
    for prog in programs(spec):
        if prog != sol.get("program"):
            claims += "\n" + source_block(recipe, prog)
    (gen / "claims.adoc").write_text(claims, encoding="utf-8")

    if "mcp" in sol:
        # mcp.json: the hosts, for a reader's MCP client; the calls are in the book.
        (recipe / "mcp.json").write_text(mcp_config(spec), encoding="utf-8")
        return
    # run.sh and run.ps1: the solution, for a reader to run.
    head = f"# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.\n# Generated by build.py from recipe.yml for recipe {spec['key']}. Do not edit.\n"
    sh = ["#!/usr/bin/env sh", head.rstrip(), "set -e", 'cd "$(dirname "$0")"']
    for s in servers:
        sh.append(s + " &")
    # A solution that is meant to fail must not end the script before the server is stopped.
    fails = "expect" in sol and sol["expect"].get("exit", 0) != 0
    sh += (["sleep 1"] if servers else []) + [sol_cmd + (" || true" if fails else "")] + (["kill %1"] if servers else [])
    (recipe / "run.sh").write_text("\n".join(sh) + "\n", encoding="utf-8")
    (recipe / "run.sh").chmod(0o755)
    ps = [head.rstrip(), "Set-Location $PSScriptRoot"]
    for i, s in enumerate(servers):
        exe, *args = s.replace("{{null}}", "NUL").split()
        ps.append(f"$server{i} = Start-Process {exe} -ArgumentList {', '.join(repr(a) for a in args)} -PassThru -NoNewWindow")
    ps_cmd = case_command(sol, {**values, "null": "NUL"})
    if ps_cmd.startswith("curl "):
        ps_cmd = "curl.exe " + ps_cmd[len("curl "):]
    if servers:
        ps.append("Start-Sleep 1")
        ps.append("try { " + ps_cmd + " } finally { "
                  + "; ".join(f"Stop-Process $server{i}" for i in range(len(servers))) + " }")
    else:
        ps.append(ps_cmd)
    (recipe / "run.ps1").write_text("\n".join(ps) + "\n", encoding="utf-8")


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


def main():
    GEN.mkdir(exist_ok=True)
    (OUT / "recipes").mkdir(parents=True, exist_ok=True)
    ident = yaml.safe_load((BOOK / "book.yml").read_text(encoding="utf-8"))
    (GEN / "identity.adoc").write_text(
        f"= {ident['title']}: {ident['subtitle']}\n{ident['author']}\n"
        f":publisher: {ident['publisher']}\n:edition: {ident['edition']}\n"
        f":copyright: {ident['copyright']}\n:revnumber: {ident['edition']}\n:revdate: {ident['year']}\n",
        encoding="utf-8")

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
