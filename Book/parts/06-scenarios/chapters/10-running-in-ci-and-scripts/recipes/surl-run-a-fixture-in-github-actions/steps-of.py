# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Prints each step of the workflow's test job: its condition, then what it runs or uses.
import sys, yaml
job = yaml.safe_load(open(sys.argv[1], encoding="utf-8"))["jobs"]["test"]
print("runs-on:", job["runs-on"], "for", ", ".join(job["strategy"]["matrix"]["os"]))
for step in job["steps"]:
    what = step.get("run", step.get("uses", "")).strip().splitlines()[0]
    print(step.get("if", "-"), "|", what)
