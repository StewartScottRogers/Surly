#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Try a claim before writing it: start surl, run commands against it, see what happens.

  python3 build/probe.py "<surl arguments with {port}>" "<command with {port}>" ["<command>" ...]

surl (SURLY_SURL) starts in a fresh temporary folder holding site/hello.txt ("hello from surl
"),
then each command runs there; a leading curl or surl is the pinned build (SURLY_CURL, SURLY_SURL).
"-" as the first argument starts no server. PROBE_FILES="rel=content|..." adds files (\n for a
new line). PROBE_SETUP="openssl req ... ;; ssh-keygen ..." runs commands first, as setup: does.
Output is cut to 400 characters a stream; PROBE_FULL=1 prints it all.
"""
import os, shutil, socket, subprocess, sys, tempfile, time

FULL = os.environ.get("PROBE_FULL") == "1"


def cut(text, n=400):
    return text if FULL or len(text) <= n else text[:n] + f"... ({len(text)} characters)"

surl = os.environ["SURLY_SURL"]; curl = os.environ["SURLY_CURL"]
root = tempfile.mkdtemp(prefix="probe-")
os.makedirs(os.path.join(root, "site"))
open(os.path.join(root, "site", "hello.txt"), "w", newline="\n").write("hello from surl\n")
for extra in os.environ.get("PROBE_FILES", "").split("|"):
    if "=" in extra:
        rel, content = extra.split("=", 1)
        p = os.path.join(root, rel)
        os.makedirs(os.path.dirname(p), exist_ok=True)
        open(p, "w", newline="\n").write(content.replace("\\n", "\n"))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from verify import free_port      # free for TCP and UDP, outside Windows' reserved UDP ranges
port = free_port()
# PROBE_SETUP runs commands before surl starts, separated by ";;" (certificates, keys), as setup: does.
for line in filter(None, (l.strip() for l in os.environ.get("PROBE_SETUP", "").split(";;"))):
    r = subprocess.run(line, shell=True, cwd=root, capture_output=True, text=True)
    print(f"setup $ {line}\n  exit {r.returncode}" + (f"  {cut(r.stderr)!r}" if r.returncode else ""))
proc = None
if sys.argv[1] != "-":
    cmd = f'"{surl}" ' + sys.argv[1].replace("{port}", str(port))
    logf = tempfile.TemporaryFile(mode="w+")
    proc = subprocess.Popen(cmd, cwd=root, stdout=logf, stderr=logf, text=True)
    end = time.time() + 10
    while time.time() < end:
        with socket.socket() as s:
            if s.connect_ex(("127.0.0.1", port)) == 0:
                break
        if proc.poll() is not None:
            break
        time.sleep(0.1)
    time.sleep(0.3)
for c in sys.argv[2:]:
    shown = c.replace("{port}", "PORT")
    c = c.replace("{port}", str(port))
    if c.startswith("curl "):
        c = f'"{curl}" ' + c[5:]
    elif c.startswith("surl "):
        c = f'"{surl}" ' + c[5:]
    try:
        r = subprocess.run(c, shell=True, cwd=root, capture_output=True, text=True, timeout=20)
        print(f"$ {shown}\n  exit {r.returncode}\n  out {r.stdout[:1200]!r}\n  err {r.stderr[:600]!r}")
    except subprocess.TimeoutExpired:
        print(f"$ {shown}\n  TIMED OUT")
if proc:
    if proc.poll() is None:
        proc.kill()
    proc.wait()
    logf.seek(0)
    print(f"surl exit {proc.returncode}\n  surl log {logf.read()[:1500]!r}")
shutil.rmtree(root, ignore_errors=True)
