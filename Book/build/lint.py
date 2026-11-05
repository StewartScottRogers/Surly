#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Structural checks on every recipe: the things an AI author must get right
and a machine can check without judgement. Semantic review is REVIEW.md.

Usage:  python3 build/lint.py
Exit:   0 clean; 1 with every finding printed as path: finding.
"""
import re, sys
from pathlib import Path
from recipes import BOOK, recipes

SECTIONS = ["==== Problem", "==== Solution", "==== Discussion", "==== See Also"]
COPYRIGHT = "Copyright (c) 2026 Stewart Rogers"


def bibliography_keys():
    text = (BOOK / "references" / "bibliography.adoc").read_text(encoding="utf-8")
    return set(re.findall(r"\[\[\[([a-z0-9-]+)\]\]\]", text))


def main():
    findings = []
    keys = {spec["key"] for _, _, _, spec in recipes()}
    bib = bibliography_keys()
    for _, _, recipe, spec in recipes():
        adoc = recipe / "recipe.adoc"
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
        for ref in spec.get("references", []):
            if ref not in bib:
                findings.append(f"{rel}: reference '{ref}' is not in the bibliography")
            elif f"<<{ref}>>" not in text:
                findings.append(f"{rel}: reference '{ref}' is listed but never cited")
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
