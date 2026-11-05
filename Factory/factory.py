#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../Book/LICENSE-CODE.txt.
"""The runner. Deliberately thin: pick the next ready task, hand it to the
configured agent with the policy as its brief, run the gates, move the task.
It never asks a question (ADR-0006).

Usage: python3 Factory/factory.py [--hours H] [--lanes N] [--once]
"""
import argparse, datetime, re, shutil, subprocess, sys, time
from pathlib import Path
import yaml

ROOT = Path(__file__).resolve().parent.parent
FACTORY = ROOT / "Factory"
TASKS = FACTORY / "Tasks"
GATES = {
    "recipe": ["python3 Book/build/lint.py", "python3 Book/build/build.py"],
    "revision": ["python3 Book/build/lint.py", "python3 Book/build/build.py"],
    "verify": ["python3 Book/build/build.py"],
    "program-task": [],
    "factory": ["python3 Book/build/lint.py", "python3 Book/build/build.py"],
}
ROLE = {"recipe": "author", "revision": "author", "verify": "verifier", "program-task": "verifier", "factory": "mechanic"}


def field(text, name):
    m = re.search(rf"^{name}:\s*(.+)$", text, re.M)
    return m.group(1).strip() if m else ""


def done_keys():
    return {field(p.read_text(encoding="utf-8"), "key") for p in (TASKS / "Done").glob("*.md")}


def next_ready():
    finished = done_keys()
    for p in sorted((TASKS / "Backlog").glob("*.md")):
        t = p.read_text(encoding="utf-8")
        if field(t, "status") != "ready":
            continue
        deps = [d.strip() for d in field(t, "depends").strip("[]").split(",") if d.strip()]
        if all(d in finished for d in deps):
            return p
    return None


def log(path, lane, message):
    with path.open("a", encoding="utf-8") as f:
        f.write(f"- {datetime.datetime.now():%Y-%m-%d %H:%M} lane-{lane}: {message}\n")


def brief(task_path, role):
    policy = "\n\n".join((FACTORY / "Policy" / n).read_text(encoding="utf-8")
                         for n in ("Defaults.md", "Gates.md", "Lanes.md", "Done.md"))
    return (f"You are a {role} lane of the Surly book factory. Read Book/AI.md, then the policy below, "
            f"then the task. Do the task. Never ask a question: take the written default, record it in the "
            f"task's Log, and if it is a question for the author append it to Factory/Decisions-Inbox.md. "
            f"When done, append your Log entry to the task file and exit 0.\n\n"
            f"TASK FILE: {task_path.relative_to(ROOT)}\n\n{task_path.read_text(encoding='utf-8')}\n\n"
            f"POLICY:\n\n{policy}\n")


def run_gates(kind, minutes):
    for cmd in GATES.get(kind, []):
        r = subprocess.run(cmd, shell=True, cwd=ROOT, capture_output=True, text=True, timeout=minutes * 60)
        if r.returncode:
            return f"gate failed: {cmd}\n{(r.stdout + r.stderr)[-2000:]}"
    return ""


def work(lane, cfg):
    task = next_ready()
    if not task:
        return False
    doing = TASKS / "Doing" / task.name
    shutil.move(task, doing)
    kind = field(doing.read_text(encoding="utf-8"), "kind") or "recipe"
    role = ROLE.get(kind, "author")
    log(doing, lane, f"started as {role}")
    r = subprocess.run(cfg["agent"], shell=True, cwd=ROOT, input=brief(doing, role), text=True,
                       capture_output=True, timeout=cfg["hours"] * 3600)
    failure = "" if r.returncode == 0 else f"agent exited {r.returncode}: {(r.stdout + r.stderr)[-2000:]}"
    failure = failure or run_gates(kind, cfg["gates_timeout_minutes"])
    if failure:
        log(doing, lane, failure.replace("\n", " ")[:1500])
        shutil.move(doing, TASKS / "Backlog" / task.name)
    else:
        log(doing, lane, "gates passed; done")
        t = doing.read_text(encoding="utf-8").replace("status: ready", "status: done", 1)
        doing.write_text(t, encoding="utf-8")
        shutil.move(doing, TASKS / "Done" / task.name)
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hours", type=float)
    ap.add_argument("--lanes", type=int)
    ap.add_argument("--once", action="store_true")
    a = ap.parse_args()
    cfg = yaml.safe_load((FACTORY / "factory.yml").read_text(encoding="utf-8"))
    if a.hours: cfg["hours"] = a.hours
    if a.lanes: cfg["lanes"] = a.lanes
    end = time.time() + cfg["hours"] * 3600
    lane = 1
    while time.time() < end:
        if not work(lane, cfg):
            print("nothing ready")
            break
        if a.once:
            break
        lane = lane % cfg["lanes"] + 1
    print("shift over")


if __name__ == "__main__":
    main()
