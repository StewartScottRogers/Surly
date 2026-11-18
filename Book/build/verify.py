#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Run every recipe's cases against the surl, curl and dotnet on PATH (or
SURLY_SURL, SURLY_CURL and SURLY_DOTNET) and write verified.yml beside each recipe.

For each recipe: make a temporary working directory, write the fixtures,
allocate a free port, start the servers and wait until each is ready, run every
case and compare exit code, stdout, stderr and files with its expectations, stop
the servers, and record the result. A recipe passes only if every case passes.

A program: case (schema 2, ADR-0008) is a C# file-based app in the recipe folder.
It is built once with `dotnet build <file>.cs`, then each case runs it with
`dotnet run --no-build <file>.cs -- <args>` in the working directory, so its
output is the program's alone. Packages come from the configured NuGet feeds;
SURLY_NUGET_SOURCE adds one folder or feed (for packages not yet published).

An mcp: case (schema 2, ADR-0008) is a list of tool calls. verify starts each
MCP host the recipe calls once (surl-mcp with --root, curl-mcp with
CURL_MCP_ROOT, both at the working directory), shared by the recipe's cases in
order, so a value one call saves ({{url}}) reaches every later call. Each call
must not be an error unless its expect: says is-error: true, and its result must
contain result-contains. The hosts' versions are the ones they report when the
handshake is done. SURLY_SURL_MCP and SURLY_CURL_MCP name them, as SURLY_SURL does.

Only the tools a recipe uses are needed, and only their versions are recorded.

Usage:  python3 build/verify.py [key ...]     (all recipes when no key is given)
Exit:   0 when every recipe passed; 1 when any failed or the binaries are missing.
Then run build.py so the book shows the new status.
"""
import datetime, os, platform, re, shutil, socket, subprocess, sys, tempfile, time
from pathlib import Path
import yaml
from recipes import (recipes, substitute, substitute_deep, posix_values, windows_values, programs,
                     program_packages, mcp_hosts, MCP_HOSTS, PLACEHOLDER)
from mcp import Host, McpError, contains, path_value

WINDOWS = os.name == "nt"
PLATFORM = {"Windows": "windows", "Linux": "linux", "Darwin": "macos"}.get(platform.system(), platform.system().lower())


def binary(name):
    variable = "SURLY_" + name.upper().replace("-", "_")
    path = os.environ.get(variable) or shutil.which(name)
    if not path:
        sys.exit(f"{name} not found: put it on PATH or set {variable}")
    return path


def tools(spec):
    """The programs a recipe needs: surl or curl at the head of a server or run line, dotnet for a program."""
    heads = [s["command"].split()[0] for s in (spec.get("servers") or {}).values()]
    heads += [c["run"].split()[0] for c in spec["cases"] if "run" in c]
    needed = {h for h in heads if h in ("surl", "curl")}
    if programs(spec):
        needed.add("dotnet")
    return needed | set(mcp_hosts(spec))


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


def build_program(path, root, bins):
    """Build a C# file-based app once; returns None, or why it would not build."""
    cmd = [bins["dotnet"], "build", str(path), "--nologo", "-v", "q"]
    source = os.environ.get("SURLY_NUGET_SOURCE")
    if source:
        cmd.append(f"--property:RestoreAdditionalProjectSources={source}")
    try:
        r = subprocess.run(cmd, cwd=root, capture_output=True, text=True, timeout=600)
    except subprocess.TimeoutExpired:
        return f"dotnet build {path.name} timed out"
    if r.returncode:
        tail = [l for l in (r.stdout + r.stderr).splitlines() if l.strip()][-5:]
        return f"dotnet build {path.name} failed: " + " | ".join(tail)
    return None


def start_host(name, root, bins):
    """Start an MCP host confined to the working directory."""
    if name == "surl-mcp":
        return Host([bins[name], "--root", str(root)], cwd=root)
    return Host([bins[name]], cwd=root, env=dict(os.environ, CURL_MCP_ROOT=str(root)))


def run_mcp_case(case, root, values, bins, hosts, saved):
    for i, call in enumerate(case["mcp"], 1):
        at = f"call {i} ({call['tool']})"
        arguments = call.get("arguments") or {}
        known = {**values, **saved}
        missing = sorted(set(PLACEHOLDER.findall(str(arguments))) - known.keys())
        if missing:
            return False, f"{at}: nothing saved as {', '.join(missing)}"
        name = MCP_HOSTS[call["host"]]
        try:
            if name not in hosts:
                hosts[name] = start_host(name, root, bins)
            is_error, value, text = hosts[name].call(call["tool"], substitute_deep(arguments, known))
        except McpError as e:
            return False, f"{at}: {e}"
        exp = call.get("expect") or {}
        if is_error != exp.get("is-error", False):
            return False, f"{at}: {'an error' if is_error else 'not an error'}, expected otherwise: {text[:300]}"
        if "error-contains" in exp and exp["error-contains"] not in text:
            return False, f"{at}: answer lacks {exp['error-contains']!r}: {text[:300]}"
        if "result-contains" in exp and not contains(value, substitute_deep(exp["result-contains"], known)):
            return False, f"{at}: answer {text[:300]} does not contain {exp['result-contains']!r}"
        for key, path in (call.get("save") or {}).items():
            got = path_value(value, path)
            if got is None:
                return False, f"{at}: nothing at {path} to save as {key}"
            saved[key] = got
    return True, ""


def run_case(case, recipe, root, values, bins, built, hosts, saved):
    write_fixtures(root, case.get("fixtures"))
    if "mcp" in case:
        return run_mcp_case(case, root, values, bins, hosts, saved)
    if "program" in case:
        path = recipe / case["program"]
        if path not in built:
            built[path] = build_program(path, root, bins)
        if built[path]:
            return False, built[path]
        cmd = [bins["dotnet"], "run", "--no-build", str(path), "--"] + [substitute(a, values) for a in case.get("args", [])]
        shell, timeout = False, case.get("timeout", 60)
    else:
        cmd = resolve(substitute(case["run"], values), bins)
        shell, timeout = True, case.get("timeout", 30)
    try:
        r = subprocess.run(cmd, shell=shell, cwd=root, capture_output=True, text=True, timeout=timeout)
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
    procs, results, failures, built, hosts, saved = [], {}, [], {}, {}, {}
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
                ok, why = run_case(case, recipe, root, values, bins, built, hosts, saved)
                results[case["id"]] = "pass" if ok else "fail"
                if not ok:
                    failures.append(f"case {case['id']}: {why}")
    finally:
        host_versions = {name: host.version for name, host in hosts.items()}
        for host in hosts.values():
            host.close()
        for p in procs:
            p.kill()
            p.wait()
        shutil.rmtree(root, ignore_errors=True)
    return results, failures, host_versions


def recipe_versions(recipe, spec, tool_versions, host_versions):
    """Every version this recipe ran against: its tools and MCP hosts, then each package its programs name."""
    found = {t: tool_versions[t] for t in ("surl", "curl", "dotnet") if t in tools(spec)}
    found.update({name: host_versions.get(name, "not started") for name in mcp_hosts(spec)})
    for program in programs(spec):
        for name, ver in program_packages(recipe / program).items():
            found[name] = ver
    return found


def main():
    wanted = set(sys.argv[1:])
    selected = [(recipe, spec) for _, _, recipe, spec in recipes() if not wanted or spec["key"] in wanted]
    bins = {t: binary(t) for t in sorted(set().union(*(tools(spec) for _, spec in selected)))}
    tool_versions = {k: version(v) for k, v in bins.items() if k not in MCP_HOSTS.values()}
    today = datetime.date.today().isoformat()
    any_failed = False
    for recipe, spec in selected:
        results, failures, host_versions = verify_recipe(recipe, spec, bins)
        status = "verified" if not failures else "broken"
        any_failed |= bool(failures)
        versions = recipe_versions(recipe, spec, tool_versions, host_versions)
        previous = spec["verified"]
        platforms = sorted(set(previous.get("platforms", []) if previous.get("status") == "verified"
                               and previous.get("versions") == versions else []) | {PLATFORM}) if status == "verified" else [PLATFORM]
        record = {"status": status, "versions": versions, "date": today,
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
