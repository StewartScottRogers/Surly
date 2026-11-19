#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Structural checks on every recipe: the things an AI author must get right
and a machine can check without judgement. Semantic review is REVIEW.md.

Usage:  python3 build/lint.py
Exit:   0 clean; 1 with every finding printed as path: finding.
"""
import re, sys
from pathlib import Path
from recipes import (BOOK, recipes, programs, program_packages, program_projects, pinned_packages, solution_case,
                     mcp_tools_used)

SECTIONS = ["==== Problem", "==== Solution", "==== Discussion", "==== See Also"]
COPYRIGHT = "Copyright (c) 2026 Stewart Rogers"


def bibliography_keys():
    text = (BOOK / "references" / "bibliography.adoc").read_text(encoding="utf-8")
    return set(re.findall(r"\[\[\[([a-z0-9-]+)\]\]\]", text))


def lint_surface(recipe, spec):
    """One recipe, one surface (ADR-0008 item 1). A console recipe's solution is a run: line;
    a csharp recipe's and an mcp recipe's is a C# program: (an mcp recipe's program drives
    surl-mcp and curl-mcp with the official MCP SDK). A run: case may sit beside either."""
    rel = f"{recipe.relative_to(BOOK).as_posix()}/recipe.yml"
    kind = {"console": "run", "csharp": "program", "mcp": "program"}[spec["surface"]]
    found = []
    if kind not in solution_case(spec):
        found.append(f"{rel}: surface {spec['surface']} needs a solution with {kind}:")
    for c in spec["cases"]:
        if "program" in c and spec["surface"] == "console":
            found.append(f"{rel}: case {c['id']} runs a C# program, so surface must be csharp or mcp")
        if "mcp" in c:
            found.append(f"{rel}: case {c['id']} uses the retired mcp: call list; write a C# program on the MCP SDK")
    if spec["surface"] == "mcp" and any("ModelContextProtocol" not in (recipe / p).read_text(encoding="utf-8")
                                        for p in programs(spec)):
        found.append(f"{rel}: an mcp recipe's programs use the MCP SDK (#:package ModelContextProtocol.Core)")
    return found


def lint_programs(recipe, spec, pins):
    """C# programs (ADR-0008): copyright, packages pinned as the book pins them, no
    project references, and Libcurl only in the advanced tier."""
    found = []
    for prog in programs(spec):
        path = recipe / prog
        rel = path.relative_to(BOOK).as_posix()
        if COPYRIGHT not in path.read_text(encoding="utf-8"):
            found.append(f"{rel}: missing copyright line")
        for project in program_projects(path):
            found.append(f"{rel}: #:project {project}: reference a package with #:package instead")
        for name, version in program_packages(path).items():
            if name not in pins:
                found.append(f"{rel}: package {name} is not pinned in build/packages.yml")
            elif pins[name] is None:
                found.append(f"{rel}: package {name} has no pinned version yet")
            elif version != str(pins[name]):
                found.append(f"{rel}: #:package {name}@{version or '(no version)'} must be {name}@{pins[name]}; run build/pin.py")
            if name == "Libcurl" and spec["tier"] != "advanced":
                found.append(f"{rel}: Libcurl recipes belong in the advanced tier, not {spec['tier']}")
    return found


def main():
    findings = []
    keys = {spec["key"] for _, _, _, spec in recipes()}
    bib = bibliography_keys()
    pins = pinned_packages()
    for _, _, recipe, spec in recipes():
        adoc = recipe / "recipe.adoc"
        if not adoc.exists():
            findings.append(f"{recipe.relative_to(BOOK).as_posix()}: recipe.yml has no recipe.adoc beside it")
            continue
        text = adoc.read_text(encoding="utf-8")
        rel = adoc.relative_to(BOOK).as_posix()
        if COPYRIGHT not in text:
            findings.append(f"{rel}: missing copyright line")
        if COPYRIGHT not in (recipe / "recipe.yml").read_text(encoding="utf-8"):
            findings.append(f"{rel.replace('.adoc', '.yml')}: missing copyright line")
        pos = [text.find(s) for s in SECTIONS]
        if -1 in pos or pos != sorted(pos):
            findings.append(f"{rel}: sections must be Problem, Solution, Discussion, See Also, in that order")
        if f"[[{spec['key']}]]" not in text:
            findings.append(f"{rel}: missing anchor [[{spec['key']}]]")
        if f"=== {spec['title']}" not in text:
            findings.append(f"{rel}: heading must be '=== {spec['title']}' to match recipe.yml")
        for inc in ("attributes", "solution", "claims"):
            if f"include::.generated/{inc}.adoc[]" not in text:
                findings.append(f"{rel}: missing include::.generated/{inc}.adoc[]")
        m = re.search(r"==== Problem\s+(.+?)\n\n", text, re.S)
        if m:
            sentences = [s for s in re.split(r"(?<=[.!?])\s+", " ".join(m.group(1).split())) if s]
            if not 1 <= len(sentences) <= 2:
                findings.append(f"{rel}: Problem must be one or two sentences, has {len(sentences)}")
        else:
            findings.append(f"{rel}: Problem paragraph not found")
        disc = text[text.find("==== Discussion"):text.find("==== See Also")]
        for opt in spec.get("surl-options", []) + spec.get("curl-options", []):
            if f"`{opt}" not in disc:
                findings.append(f"{rel}: option {opt} is used but never explained in the Discussion")
        for api_type in spec.get("api-types", []):
            if f"`{api_type}" not in disc:
                findings.append(f"{rel}: type {api_type} is used but never explained in the Discussion")
        for tool in spec.get("mcp-tools", []):
            if f"`{tool}" not in disc:
                findings.append(f"{rel}: tool {tool} is used but never explained in the Discussion")
        for tool in mcp_tools_used(spec, recipe):
            if tool not in spec.get("mcp-tools", []):
                findings.append(f"{rel}: tool {tool} is called but not listed in mcp-tools")
        findings += lint_surface(recipe, spec)
        findings += lint_programs(recipe, spec, pins)
        for ref in spec.get("references", []):
            if ref not in bib:
                findings.append(f"{rel}: reference '{ref}' is not in the bibliography")
            elif f"<<{ref}>>" not in text:
                findings.append(f"{rel}: reference '{ref}' is listed but never cited")
        # `surl`'s renders as a broken code span (backtick then apostrophe starts a curly quote);
        # write `surl`{apos}s instead.
        if re.search(r"`[^`\n]+`'s\b", text):
            findings.append(f"{rel}: a code span followed by 's renders broken; write `name`{{apos}}s")
        for xref in re.findall(r"<<([a-z0-9-]+)>>", text):
            if xref not in keys and xref not in bib:
                findings.append(f"{rel}: <<{xref}>> points at no recipe or reference yet")
        ids = [c["id"] for c in spec["cases"]]
        if len(ids) != len(set(ids)):
            findings.append(f"{rel}: duplicate case ids")
        if len(spec["cases"]) < 2:
            findings.append(f"{rel}: only the solution is checked; the Discussion's claims need cases")
    for f in findings:
        print(f)
    print(f"{len(findings)} finding(s)")
    sys.exit(1 if any(not f.endswith("yet") for f in findings) else 0)


if __name__ == "__main__":
    main()
