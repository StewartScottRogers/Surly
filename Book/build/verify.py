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

An mcp recipe's cases are C# programs on the official MCP SDK (ADR-0008 item 7,
amended 2026-11-19) that start surl-mcp and curl-mcp by name. verify finds the
hosts on PATH or through SURLY_SURL_MCP and SURLY_CURL_MCP, puts shims for them
first on PATH for those programs, and records the version each host reports in
the MCP handshake.

Only the tools a recipe uses are needed, and only their versions are recorded.

Usage:  python3 build/verify.py [-j N] [key ...]   (all recipes when no key is given;
        -j N or SURLY_JOBS=N runs N recipes side by side)
Exit:   0 when every recipe passed; 1 when any failed or the binaries are missing.
Then run build.py so the book shows the new status.
"""
import datetime, os, platform, re, shlex, shutil, socket, subprocess, sys, tempfile, time
from pathlib import Path
import yaml
from recipes import (PORT_NAMES, recipes, substitute, posix_values, windows_values, programs,
                     program_packages, mcp_hosts, MCP_HOSTS)
from mcp import Host, McpError

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
    heads += [c.split()[0] for c in (spec.get("setup") or []) + (spec.get("seed") or [])]
    heads += [c["run"].split()[0] for c in spec["cases"] if "run" in c]
    needed = {h for h in heads if h in ("surl", "curl")}
    if programs(spec):
        needed.add("dotnet")
    if mcp_hosts(spec):
        needed.add("dotnet")                     # an mcp recipe's C# programs
    return needed | set(mcp_hosts(spec))


host_shims, host_versions_seen, BINS = {}, {}, {}


def hosts_on_path(bins):
    """A folder with surl-mcp and curl-mcp shims that run the pinned hosts, so a recipe's C#
    program can start them by name, as a reader's would from PATH. Made once per run."""
    if "dir" not in host_shims:
        folder = Path(tempfile.mkdtemp(prefix="surly-mcp-hosts-"))
        for name in MCP_HOSTS.values():
            if name in bins:
                if WINDOWS:
                    (folder / f"{name}.cmd").write_text(f'@"{bins[name]}" %*' + chr(13) + chr(10), encoding="utf-8")
                else:
                    shim = folder / name
                    shim.write_text(f'#!/bin/sh{chr(10)}exec "{bins[name]}" "$@"{chr(10)}', encoding="utf-8")
                    shim.chmod(0o755)
        host_shims["dir"] = folder
    return host_shims["dir"]


def host_version(name, bins):
    """The version an MCP host reports in the initialize handshake (curl-mcp has no --version)."""
    if name not in host_versions_seen:
        root = Path(tempfile.mkdtemp(prefix="surly-mcp-version-"))
        try:
            host = Host([bins[name]] + (["--root", str(root)] if name == "surl-mcp" else []), cwd=root,
                        env=None if name == "surl-mcp" else dict(os.environ, CURL_MCP_ROOT=str(root)))
            host_versions_seen[name] = host.version
            host.close()
        except McpError as e:
            host_versions_seen[name] = f"unknown ({e})"
        finally:
            shutil.rmtree(root, ignore_errors=True)
    return host_versions_seen[name]


def version(path):
    out = subprocess.run([path, "--version"], capture_output=True, text=True).stdout
    m = re.match(r"\S+\s+(\S+)", out)
    return m.group(1) if m else out.strip().split("\n")[0]


def free_port():
    """A port free for both TCP and UDP: DNS, TFTP, CoAP and QUIC listen on UDP, and Windows
    reserves UDP ranges that are free for TCP, where a UDP bind fails (surl exit 45)."""
    # The OS's own choice (port 0) comes from the ephemeral range, which Windows' UDP reservations
    # overlap heavily, so pick at random below it and check both protocols.
    import random
    for _ in range(500):
        port = random.randint(20000, 44999)
        try:
            with socket.socket() as tcp, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp:
                tcp.bind(("127.0.0.1", port))
                udp.bind(("127.0.0.1", port))
            return port
        except OSError:
            continue
    raise RuntimeError("no port free for both TCP and UDP after 500 tries")


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
        p.write_text(content, encoding="utf-8", newline="")   # exactly as written: no CRLF on Windows


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


# The MCP client: McpBridge.cs (the official C# SDK) unless SURLY_MCP_CLIENT=python asks for
# the original standard-library client.


def run_case(case, recipe, root, values, bins, built, program_env=None):
    write_fixtures(root, case.get("fixtures"))
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
        r = subprocess.run(cmd, shell=shell, cwd=root, capture_output=True, text=True, timeout=timeout,
                           env=program_env if "program" in case else None)
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


def start_server(name, server, root, values, ports, bins):
    """Start one server and wait until it is ready; returns (process, why it failed or None)."""
    cmd = resolve(substitute(server["command"], values), bins)
    # No shell: killing a shell leaves the server running on Windows. Output goes to a
    # temporary file, since a pipe nobody reads fills up and stalls a talkative server.
    log = tempfile.TemporaryFile()
    p = subprocess.Popen(cmd if WINDOWS else shlex.split(cmd), cwd=root, stdout=log, stderr=log)
    # ready: port waits for every port placeholder the command names (TCP only).
    waits = [ports[n] for n in PORT_NAMES if "{{" + n + "}}" in server["command"]]
    missed = [port for port in waits if not wait_port(port)] if server.get("ready", "port") == "port" else []
    if missed:
        return p, f"server {name} never listened on {', '.join(map(str, missed))}"
    time.sleep(0.2)
    return p, None


def run_lines(lines, what, root, values, bins):
    """Run setup: or seed: command lines in the working directory; each must exit 0."""
    for line in lines or []:
        cmd = resolve(substitute(line, values), bins)
        r = subprocess.run(cmd, shell=True, cwd=root, capture_output=True, text=True, timeout=120)
        if r.returncode:
            return f"{what} `{line}` exited {r.returncode}: {(r.stderr or r.stdout).strip()[-300:]}"
    return None


def verify_recipe(recipe, spec, bins):
    root = Path(tempfile.mkdtemp(prefix=f"surly-{spec['key']}-"))
    ports = {name: free_port() for name in PORT_NAMES}
    values = windows_values(ports) if WINDOWS else posix_values(ports)
    write_fixtures(root, spec.get("fixtures"))
    servers = spec.get("servers") or {}
    procs, results, failures, built = {}, {}, [], {}
    # An mcp recipe's programs find surl-mcp and curl-mcp on PATH, as a reader's would.
    program_env = None
    if mcp_hosts(spec):
        program_env = dict(os.environ, PATH=str(hosts_on_path(bins)) + os.pathsep + os.environ.get("PATH", ""))
    try:
        # setup: preparation (certificates, keys) before any server starts; none is a claim.
        why = run_lines(spec.get("setup"), "setup", root, values, bins)
        if why:
            failures.append(why)
        for name, server in ([] if failures else servers.items()):
            procs[name], why = start_server(name, server, root, values, ports, bins)
            if why:
                failures.append(why)
                break
        # A server that has already exited failed to start (45: its port was taken). Say so,
        # rather than let its clients fail later with a refused connection.
        for name, p in procs.items():
            if p.poll() is not None:
                failures.append(f"server {name} exited at start with {p.returncode}")
        # seed: puts things into the running servers (mail, files, messages) before the first
        # case, the Solution included; none is a claim.
        if not failures:
            why = run_lines(spec.get("seed"), "seed", root, values, bins)
            if why:
                failures.append(why)
        if not failures:
            for case in spec["cases"]:
                # restart: stops the named server and starts it again, same command and ports,
                # before this case: what a server keeps across a restart becomes a claim.
                for name in case.get("restart") or []:
                    procs[name].kill()
                    procs[name].wait()
                    procs[name], why = start_server(name, servers[name], root, values, ports, bins)
                    if why or procs[name].poll() is not None:
                        failures.append(f"case {case['id']}: restarting {name}: {why or 'it exited'}")
                        break
                else:
                    ok, why = run_case(case, recipe, root, values, bins, built, program_env)
                    results[case["id"]] = "pass" if ok else "fail"
                    if not ok:
                        failures.append(f"case {case['id']}: {why}")
                    continue
                results[case["id"]] = "fail"
                break
    finally:
        for p in procs.values():
            p.kill()
            p.wait()
        shutil.rmtree(root, ignore_errors=True)
    return results, failures


def recipe_versions(recipe, spec, tool_versions):
    """Every version this recipe ran against: its tools and MCP hosts, then each package its programs name."""
    found = {t: tool_versions[t] for t in ("surl", "curl", "dotnet") if t in tools(spec)}
    found.update({name: host_version(name, BINS) for name in mcp_hosts(spec)})
    for program in programs(spec):
        for name, ver in program_packages(recipe / program).items():
            found[name] = ver
    return found


def check_one(recipe, spec, bins, tool_versions, today):
    """Verify one recipe and write its verified.yml; returns the lines to print and whether it failed."""
    lines = []
    results, failures = verify_recipe(recipe, spec, bins)
    if any(f.endswith("exited at start with 45") for f in failures):
        # Another program took a free port between allocation and bind: not the recipe's
        # fault. Try once more on fresh ports, and say so.
        lines.append(f"{spec['key']}: a server's port was taken; retrying once on fresh ports")
        results, failures = verify_recipe(recipe, spec, bins)
    status = "verified" if not failures else "broken"
    versions = recipe_versions(recipe, spec, tool_versions)
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
    lines.append(f"{spec['key']}: {status}" + ("" if not failures else "\n  " + "\n  ".join(failures)))
    return lines, bool(failures)


def main():
    args = sys.argv[1:]
    jobs = int(os.environ.get("SURLY_JOBS", "1"))
    if args[:1] in (["-j"], ["--jobs"]):
        jobs, args = int(args[1]), args[2:]
    wanted = set(args)
    selected = [(recipe, spec) for _, _, recipe, spec in recipes(wanted or None)]
    bins = {t: binary(t) for t in sorted(set().union(*(tools(spec) for _, spec in selected)))}
    BINS.update(bins)
    tool_versions = {k: version(v) for k, v in bins.items() if k not in MCP_HOSTS.values()}
    today = datetime.date.today().isoformat()
    any_failed = False
    if jobs <= 1:
        for recipe, spec in selected:
            lines, failed = check_one(recipe, spec, bins, tool_versions, today)
            any_failed |= failed
            print("\n".join(lines), flush=True)
    else:
        # Recipes are independent (own temporary folder, own free ports, own servers), so
        # they can run side by side; results print in book order as each one finishes.
        from concurrent.futures import ThreadPoolExecutor
        with ThreadPoolExecutor(max_workers=jobs) as pool:
            futures = [pool.submit(check_one, recipe, spec, bins, tool_versions, today) for recipe, spec in selected]
            for future in futures:
                lines, failed = future.result()
                any_failed |= failed
                print("\n".join(lines), flush=True)
    sys.exit(1 if any_failed else 0)


if __name__ == "__main__":
    main()
