#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Build the book: read every recipe's metadata, generate the spine, the machine
index and the per-protocol index, then render with Asciidoctor.

Usage:  python3 build/build.py          (from the Book folder or anywhere)
Needs:  python3 with PyYAML; asciidoctor on PATH (npm install -g asciidoctor)
Output: generated/  (identity.adoc, spine.adoc, index.json, by-protocol.adoc)
        out/surly.html and out/recipes/<key>.html
"""
import json, re, subprocess, sys
from pathlib import Path
import yaml

BOOK = Path(__file__).resolve().parent.parent
GEN, OUT = BOOK / "generated", BOOK / "out"
ATTR = re.compile(r"^:([a-z-]+):\s*(.*)$")
TITLE = re.compile(r"^={1,4}\s+(.+)$")
LIST_ATTRS = {"protocols", "surl-options", "curl-options", "exit-codes", "references"}

def read_attrs(path):
    attrs, title = {}, None
    for line in path.read_text(encoding="utf-8").splitlines():
        if (m := ATTR.match(line)):
            k, v = m.group(1), m.group(2).strip()
            attrs[k] = [s.strip() for s in v.split(",") if s.strip()] if k in LIST_ATTRS else v
        elif title is None and (m := TITLE.match(line)):
            title = m.group(1).strip()
    return attrs, title

def problem_sentence(path):
    text = path.read_text(encoding="utf-8")
    m = re.search(r"==== Problem\s+(.+?)\n\n", text, re.S)
    return " ".join(m.group(1).split()) if m else ""

def main():
    GEN.mkdir(exist_ok=True); (OUT / "recipes").mkdir(parents=True, exist_ok=True)
    ident = yaml.safe_load((BOOK / "book.yml").read_text(encoding="utf-8"))
    (GEN / "identity.adoc").write_text(
        f"= {ident['title']}: {ident['subtitle']}\n{ident['author']}\n"
        f":publisher: {ident['publisher']}\n:edition: {ident['edition']}\n"
        f":copyright: {ident['copyright']}\n:revnumber: {ident['edition']}\n:revdate: {ident['year']}\n",
        encoding="utf-8")

    spine, index, by_proto = [], [], {}
    part_n = 0
    for part in sorted((BOOK / "parts").iterdir()):
        if not (part / "part.adoc").exists(): continue
        part_n += 1
        spine.append(f"include::../{part.relative_to(BOOK).as_posix()}/part.adoc[]\n\n")
        for chapter in sorted((part / "chapters").iterdir()):
            if not (chapter / "chapter.adoc").exists(): continue
            spine.append(f"include::../{chapter.relative_to(BOOK).as_posix()}/chapter.adoc[]\n\n")
            for recipe in sorted((chapter / "recipes").iterdir()):
                rfile = recipe / "recipe.adoc"
                if not rfile.exists(): continue
                attrs, title = read_attrs(rfile)
                key = attrs.get("recipe-key", recipe.name)
                if key != recipe.name:
                    sys.exit(f"{rfile}: recipe-key '{key}' must equal the folder name '{recipe.name}'")
                rel = rfile.relative_to(BOOK).as_posix()
                spine.append(f"include::../{rel}[]\n\n")
                entry = {"key": key, "title": title, "problem": problem_sentence(rfile),
                         "part": part.name, "chapter": chapter.name, "source": rel,
                         "commands": f"{recipe.relative_to(BOOK).as_posix()}/run.sh",
                         "rendered": f"out/recipes/{key}.html", **attrs}
                entry.pop("recipe-key", None)
                index.append(entry)
                for p in attrs.get("protocols", []): by_proto.setdefault(p, []).append(key)
                # One page per recipe, with the book's identity and attributes so numbering and xrefs resolve.
                single = GEN / f"recipe-{key}.adoc"
                single.write_text("include::identity.adoc[]\n:doctype: article\n:xrefstyle: short\n"
                                  ":icons: font\n:source-highlighter: highlight.js\n:experimental:\n\n"
                                  f"include::../{rel}[leveloffset=-1]\n\ninclude::../references/bibliography.adoc[]\n",
                                  encoding="utf-8")
                run(["asciidoctor", "-a", "allow-uri-read", "-o", str(OUT / "recipes" / f"{key}.html"), str(single)])

    (GEN / "spine.adoc").write_text("".join(spine), encoding="utf-8")
    (GEN / "index.json").write_text(json.dumps({"book": ident, "recipes": index}, indent=2), encoding="utf-8")
    lines = ["// Generated. Do not edit.\n[appendix]\n== Recipes by Protocol\n"]
    for p in sorted(by_proto):
        lines.append(f"\n=== {p}\n\n" + "".join(f"* <<{k}>>\n" for k in by_proto[p]))
    (GEN / "by-protocol.adoc").write_text("".join(lines), encoding="utf-8")
    run(["asciidoctor", "-o", str(OUT / "surly.html"), str(BOOK / "book.adoc")])
    print(f"{len(index)} recipe(s); book at {OUT / 'surly.html'}; index at {GEN / 'index.json'}")

def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode: sys.exit(r.stderr or r.stdout)
    if r.stderr.strip(): print(r.stderr.strip(), file=sys.stderr)

if __name__ == "__main__": main()
