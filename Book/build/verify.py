#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Run every recipe's cases against the surl and curl on PATH (or SURLY_SURL and
SURLY_CURL) and write verified.yml beside each recipe.

For each recipe: make a temporary working directory, write the fixtures,
allocate a free port, start the servers and wait until each is ready, run every
case and compare exit code, stdout, stderr and files with its expectations, stop
the servers, and record the result. A recipe passes only if every case passes.

Usage:  python3 build/verify.py [key ...]     (all recipes when no key is given)
Exit:   0 when every recipe passed; 1 when any failed or the binaries are missing.
Then run build.py so the book shows the new status.
"""
import datetime, os, platform, re, shutil, socket, subprocess, sys, tempfile, time
from pathlib import Path
import yaml
from recipes import recipes, substitute, posix_values, windows_values

WINDOWS = os.name == "nt"
PLATFORM = {"Windows": "windows", "Linux": "linux", "Darwin": "macos"}.get(platform.system(), platform.system().lower())


def binary(name):
    path = os.environ.get(f"SURLY_{name.upper()}") or shutil.which(name)
    if not path:
        sys.exit(f"{name} not found: put it on PATH or set SURLY_{name.upper()}")
    return path


def version(path):
    out = subprocess.run([path, "--version"], capture_output=True, text=True).stdout
    m = re.match(r"\S+\s+(\S+)", out)
    return m.group(1) if m else out.strip().split("\n")[0]


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def wait_port(port, seconds=10):
    end = time.time() + seconds
    while time.time() < end:
        with socket.socket() as s:
            s.settimeout(0.2)
            if s.connect_ex(("127.0.0.1", port)) == 0:
                return True
        time.sleep(0.1)
    return False


def write_fixtures(root, fixtures):
    for rel, content in (fixtures or {}).items():
        p = root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(content, encoding="utf-8")


def resolve(cmd, bins):
    """Replace a leading surl/curl with the pinned binary."""
    exe, _, rest = cmd.partition(" ")
    return f'"{bins[exe]}" {rest}' if exe in bins else cmd


def run_case(case, root, values, bins):
    write_fixtures(root, case.get("fixtures"))
    cmd = resolve(substitute(case["run"], values), bins)
    try:
        r = subprocess.run(cmd, shell=True, cwd=root, capture_output=True, text=True,
                           timeout=case.get("timeout", 30))
    except subprocess.TimeoutExpired:
        return False, "timed out"
    exp = case["expect"]
    problems = []
    if r.returncode != exp.get("exit", 0):
        problems.append(f"exit {r.returncode}, expected {exp.get('exit', 0)}")
    if "stdout" in exp and r.stdout != exp["stdout"]:
        problems.append(f"stdout {r.stdout!r}, expected {exp['stdout']!r}")
    if "stdout-matches" in exp and not re.search(exp["stdout-matches"], r.stdout, re.S):
        problems.append(f"stdout {r.stdout!r} does not match /{exp['stdout-matches']}/")
    if "stderr-contains" in exp and exp["stderr-contains"] not in r.stderr:
        problems.append(f"stderr {r.stderr!r} lacks {exp['stderr-contains']!r}")
    for rel, content in (exp.get("files") or {}).items():
        p = root / rel
        if not p.exists():
            problems.append(f"file {rel} missing")
        elif content is not None and p.read_text(encoding="utf-8") != content:
            problems.append(f"file {rel} is {p.read_text(encoding='utf-8')!r}, expected {content!r}")
    return (not problems), "; ".join(problems)


def verify_recipe(recipe, spec, bins):
    root = Path(tempfile.mkdtemp(prefix=f"surly-{spec['key']}-"))
    port = free_port()
    values = windows_values(port) if WINDOWS else posix_values(port)
    write_fixtures(root, spec.get("fixtures"))
    procs, results, failures = [], {}, []
    try:
        for name, server in (spec.get("servers") or {}).items():
            cmd = resolve(substitute(server["command"], values), bins)
            p = subprocess.Popen(cmd, shell=True, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            procs.append(p)
            ready = server.get("ready", "port")
            if ready == "port" and not wait_port(port):
                failures.append(f"server {name} never listened on {port}")
                break
            time.sleep(0.2)
        if not failures:
            for case in spec["cases"]:
                ok, why = run_case(case, root, values, bins)
                results[case["id"]] = "pass" if ok else "fail"
                if not ok:
                    failures.append(f"case {case['id']}: {why}")
    finally:
        for p in procs:
            p.kill()
            p.wait()
        shutil.rmtree(root, ignore_errors=True)
    return results, failures


def main():
    bins = {"surl": binary("surl"), "curl": binary("curl")}
    versions = {k: version(v) for k, v in bins.items()}
    wanted = set(sys.argv[1:])
    today = datetime.date.today().isoformat()
    any_failed = False
    for _, _, recipe, spec in recipes():
        if wanted and spec["key"] not in wanted:
            continue
        results, failures = verify_recipe(recipe, spec, bins)
        status = "verified" if not failures else "broken"
        any_failed |= bool(failures)
        previous = spec["verified"]
        platforms = sorted(set(previous.get("platforms", []) if previous.get("status") == "verified"
                               and previous.get("surl") == versions["surl"] and previous.get("curl") == versions["curl"]
                               else []) | {PLATFORM}) if status == "verified" else [PLATFORM]
        record = {"status": status, "surl": versions["surl"], "curl": versions["curl"], "date": today,
                  "platforms": platforms, "cases": results}
        if failures:
            record["failures"] = failures
        (recipe / "verified.yml").write_text(
            "# Written by build/verify.py. Do not edit; re-run verify.py instead.\n"
            + yaml.safe_dump(record, sort_keys=False), encoding="utf-8")
        print(f"{spec['key']}: {status}" + ("" if not failures else "\n  " + "\n  ".join(failures)))
    sys.exit(1 if any_failed else 0)


if __name__ == "__main__":
    main()
