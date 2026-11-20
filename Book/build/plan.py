#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""The book's plan: every recipe, written or to be written, in editorial/plan.yml.

  python3 build/plan.py status                 counts by part, surface and status; the next batch
  python3 build/plan.py check                  the plan and the recipe folders agree
  python3 build/plan.py scaffold <key> ...     make each planned recipe's folder from its template
  python3 build/plan.py init                   write plan.yml from the recipes on disk (once)

A row: key, title, part, chapter, surface (console | csharp | mcp), protocols, task (one line:
what the reader wants), batch, status (planned | drafted | accepted), and optionally triplet
(a use case shared with its other two surfaces). verified comes from each verified.yml, not here.
A fresh session reads this file and the writer guide, not the conversation that made them.
"""
import re, shutil, sys
from collections import Counter
from pathlib import Path
import yaml
from recipes import BOOK, TIERS, pinned_packages, recipes

PLAN = BOOK / "editorial" / "plan.yml"
TEMPLATES = {"console": "recipe-template", "csharp": "recipe-template-csharp", "mcp": "recipe-template-mcp"}
HEAD = ("# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.\n"
        "# The book's plan: one row per recipe, written or planned. See build/plan.py.\n")


def load():
    return (yaml.safe_load(PLAN.read_text(encoding="utf-8")) or {}).get("recipes", []) if PLAN.exists() else []


def save(rows):
    PLAN.write_text(HEAD + yaml.safe_dump({"recipes": rows}, sort_keys=False, allow_unicode=True, width=120),
                    encoding="utf-8")


def tier_of(part):
    """01-foundations -> foundations: a part folder names its tier."""
    name = re.sub(r"^\d+-", "", part)
    return name if name in TIERS else "scenarios" if "scenario" in name else "new-protocols"


def init():
    if PLAN.exists():
        sys.exit(f"{PLAN.name} exists; edit it")
    rows = []
    for part, chapter, recipe, spec in recipes():
        problem = re.search(r"==== Problem\s+(.+?)\n\n", (recipe / "recipe.adoc").read_text(encoding="utf-8"), re.S)
        rows.append({"key": spec["key"], "title": spec["title"], "part": part.name, "chapter": chapter.name,
                     "surface": spec["surface"], "protocols": spec["protocols"],
                     "task": " ".join(problem.group(1).split()) if problem else "",
                     "batch": f"part-{int(part.name[:2])}", "status": "accepted"})
    save(rows)
    print(f"{len(rows)} recipes written to {PLAN.relative_to(BOOK)}")


def status():
    rows = load()
    by = Counter((r["status"], r["surface"]) for r in rows)
    print(f"{len(rows)} recipes in the plan")
    for s in ("accepted", "drafted", "planned"):
        line = ", ".join(f"{by[(s, f)]} {f}" for f in ("console", "csharp", "mcp") if by[(s, f)])
        if line:
            print(f"  {s}: {line}")
    parts = Counter(r["part"] for r in rows)
    print("  by part: " + ", ".join(f"{p} {n}" for p, n in sorted(parts.items())))
    planned = [r for r in rows if r["status"] == "planned"]
    if planned:
        batch = planned[0]["batch"]
        print(f"  next batch {batch}: {sum(1 for r in planned if r['batch'] == batch)} recipes")


def check():
    rows = {r["key"]: r for r in load()}
    on_disk = {spec["key"]: (part.name, chapter.name, spec) for part, chapter, _, spec in recipes()}
    problems = []
    for key, (part, chapter, spec) in on_disk.items():
        r = rows.get(key)
        if not r:
            problems.append(f"{key}: on disk but not in the plan")
        elif (r["part"], r["chapter"], r["surface"]) != (part, chapter, spec["surface"]):
            problems.append(f"{key}: the plan says {r['part']}/{r['chapter']} {r['surface']}, the folder says "
                            f"{part}/{chapter} {spec['surface']}")
        elif r["status"] == "planned":
            problems.append(f"{key}: written but still planned")
    for key, r in rows.items():
        if key not in on_disk and r["status"] != "planned":
            problems.append(f"{key}: {r['status']} in the plan but has no recipe folder")
    print("\n".join(problems) or f"plan and folders agree ({len(rows)} recipes)")
    sys.exit(1 if problems else 0)


def scaffold(keys):
    rows = {r["key"]: r for r in load()}
    pins = pinned_packages()
    for key in keys:
        r = rows.get(key) or sys.exit(f"{key} is not in the plan")
        chapter_dir = BOOK / "parts" / r["part"] / "chapters" / r["chapter"]
        target = chapter_dir / "recipes" / key
        if target.exists():
            print(f"{key}: exists, left alone")
            continue
        if not (chapter_dir / "chapter.adoc").exists():
            chapter_dir.mkdir(parents=True, exist_ok=True)
            title = r.get("chapter_title") or r["chapter"].split("-", 1)[-1].replace("-", " ").title()
            (chapter_dir / "chapter.adoc").write_text(
                "// Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. See ../../../../LICENSE.txt.\n"
                f"== {title}\n\nONE OR TWO SENTENCES SAYING WHAT THE CHAPTER'S RECIPES HAVE IN COMMON.\n", encoding="utf-8")
        shutil.copytree(BOOK / "templates" / TEMPLATES[r["surface"]], target)
        for f in target.iterdir():
            text = f.read_text(encoding="utf-8")
            text = text.replace("KEY", key).replace("TITLE AS A VERB PHRASE", r["title"])
            text = text.replace("protocols: [PROTOCOL]", "protocols: [" + ", ".join(r["protocols"]) + "]")
            text = re.sub(r"^tier: \S+", f"tier: {tier_of(r['part'])}", text, flags=re.M)
            text = re.sub(r"#:package ([A-Za-z0-9_.]+)@VERSION",
                          lambda m: f"#:package {m.group(1)}@{pins.get(m.group(1)) or 'VERSION'}", text)
            if f.name == "recipe.adoc" and r.get("task"):
                text = text.replace("One or two sentences, from the reader's point of view, ending in what they want.",
                                    r["task"])
            f.write_text(text, encoding="utf-8")
        r["status"] = "drafted"
        print(f"{key}: scaffolded in {target.relative_to(BOOK)}")
    save(list(rows.values()))


if __name__ == "__main__":
    command, args = (sys.argv[1], sys.argv[2:]) if len(sys.argv) > 1 else ("status", [])
    {"init": lambda: init(), "status": lambda: status(), "check": lambda: check(),
     "scaffold": lambda: scaffold(args)}.get(command, lambda: sys.exit(__doc__))()
