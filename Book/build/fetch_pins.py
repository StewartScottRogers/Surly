#!/usr/bin/env python3
# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See ../LICENSE-CODE.txt.
"""Get the pinned Surl and Curl (build/pins.yml) ready for verify, on this machine or in CI.

For each program: when one of its GitHub releases is tagged at the pinned commit, download that
release's binaries for this platform (checked against its SHA256SUMS); otherwise build them from
source as CI always did. The three libraries (Surl.Api, Curl.Api, Libcurl) are always packed from
source, since the releases carry none at a stable version. Writes <out>/env.sh for a shell, and with
--github-env exports the same values to a GitHub Actions job.

Usage:  python3 build/fetch_pins.py --out DIR --surl-src PATH --curl-src PATH [--github-env] [--source-only]
                [--surl-commit SHA] [--curl-commit SHA]   (override pins.yml, as CI's surl_ref/curl_ref do)
        A source PATH is a checkout at the pinned commit (CI) or any clone that has it (git archive is used).
Then:   source DIR/env.sh && python3 build/verify.py ...
"""
import argparse, hashlib, json, os, platform, re, shutil, subprocess, sys, tarfile, zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPOS = {"surl": "StewartScottRogers/Surl", "curl": "StewartScottRogers/Curl"}
WINDOWS = os.name == "nt"
EXE = ".exe" if WINDOWS else ""


def rid():
    machine = platform.machine().lower()
    arch = "arm64" if machine in ("arm64", "aarch64") else "x64"
    osname = {"Windows": "win", "Linux": "linux", "Darwin": "osx"}[platform.system()]
    return f"{osname}-{arch}"


def pins():
    text = (HERE / "pins.yml").read_text(encoding="utf-8")
    found = dict(re.findall(r"^(surl|curl):\s*([0-9a-f]{40})", text, re.M))
    if set(found) != {"surl", "curl"}:
        sys.exit("pins.yml must name a 40-character surl and curl commit")
    return found


def run(cmd, **kw):
    print("+", " ".join(str(c) for c in cmd), flush=True)
    return subprocess.run(cmd, check=True, **kw)


def gh_json(args):
    r = subprocess.run(["gh", *args], capture_output=True, text=True)
    return json.loads(r.stdout) if r.returncode == 0 and r.stdout.strip() else None


def release_at(repo, commit):
    """The tag of a release of repo whose tag points at commit, or None."""
    releases = gh_json(["release", "list", "-R", repo, "--limit", "20", "--json", "tagName"]) or []
    for rel in releases:
        tag = rel["tagName"]
        sha = gh_json(["api", f"repos/{repo}/commits/{tag}", "--jq", "{sha: .sha}"])
        if sha and sha.get("sha") == commit:
            return tag
    return None


def download(repo, tag, patterns, into):
    into.mkdir(parents=True, exist_ok=True)
    args = ["gh", "release", "download", tag, "-R", repo, "-D", str(into), "--clobber", "-p", "SHA256SUMS"]
    for p in patterns:
        args += ["-p", p]
    run(args)
    sums = dict(reversed(line.split()) for line in (into / "SHA256SUMS").read_text().splitlines() if line.strip())
    got = [f for f in into.iterdir() if f.name != "SHA256SUMS"]
    for f in got:
        want = sums.get(f.name)
        if want and hashlib.sha256(f.read_bytes()).hexdigest() != want:
            sys.exit(f"{f.name}: SHA-256 does not match the release's SHA256SUMS")
    return got


def unpack(archive, dest, inner_prefix=""):
    """Extract archive into dest, keeping only entries under inner_prefix (with it stripped)."""
    dest.mkdir(parents=True, exist_ok=True)
    if archive.name.endswith((".zip", ".nupkg")):
        with zipfile.ZipFile(archive) as z:
            for info in z.infolist():
                name = info.filename
                if info.is_dir() or not name.startswith(inner_prefix):
                    continue
                target = dest / name[len(inner_prefix):]
                target.parent.mkdir(parents=True, exist_ok=True)
                with z.open(info) as src, open(target, "wb") as out:
                    shutil.copyfileobj(src, out)
        # A zip (and so a .nupkg) keeps no Unix permissions; Curl.Mcp has a dot in its name, so no
        # test on the extension will do: mark every extracted file executable.
        if not WINDOWS:
            for f in dest.rglob("*"):
                if f.is_file():
                    f.chmod(0o755)
    else:
        with tarfile.open(archive) as t:
            t.extractall(dest, filter="data")   # a tar keeps the release's own permissions


def source(name, src, commit, out):
    """A directory holding the program's source at commit."""
    src = Path(src)
    head = subprocess.run(["git", "-C", str(src), "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
    status = subprocess.run(["git", "-C", str(src), "status", "--porcelain"], capture_output=True, text=True).stdout.strip()
    if head == commit and not status:
        return src
    dest = out / "src" / name
    if dest.exists():
        shutil.rmtree(dest)
    dest.mkdir(parents=True)
    archive = subprocess.run(["git", "-C", str(src), "archive", commit], capture_output=True, check=True).stdout
    subprocess.run(["tar", "-x", "-C", str(dest)], input=archive, check=True)
    return dest


def build(project, into):
    run(["dotnet", "build", str(project), "-c", "Release", "-m:1", "-o", str(into), "-v", "q", "--nologo"])


def pack(project, feed):
    run(["dotnet", "pack", str(project), "-c", "Release", "-m:1",
         "-p:BundledProjectReferencesPrivateAssets=all", "-o", str(feed), "-v", "q", "--nologo"])


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out", required=True)
    ap.add_argument("--surl-src", required=True)
    ap.add_argument("--curl-src", required=True)
    ap.add_argument("--github-env", action="store_true")
    ap.add_argument("--source-only", action="store_true", help="build every binary from source, ignoring releases")
    ap.add_argument("--surl-commit", help="the Surl commit to use instead of pins.yml's (CI's surl_ref input)")
    ap.add_argument("--curl-commit", help="the Curl commit to use instead of pins.yml's (CI's curl_ref input)")
    a = ap.parse_args()

    out = Path(a.out).resolve()
    bin_, feed = out / "bin", out / "feed"
    for d in (bin_, feed):
        if d.exists():
            shutil.rmtree(d)
        d.mkdir(parents=True)
    pin, r = pins(), rid()
    pin = {"surl": a.surl_commit or pin["surl"], "curl": a.curl_commit or pin["curl"]}
    src = {"surl": source("Surl", a.surl_src, pin["surl"], out), "curl": source("Curl", a.curl_src, pin["curl"], out)}
    how = {}

    # Surl: surl and surl-mcp
    tag = None if a.source_only else release_at(REPOS["surl"], pin["surl"])
    if tag:
        ext = "zip" if r.startswith("win") else "tar.gz"
        dl = out / "download" / "surl"
        download(REPOS["surl"], tag, [f"surl-{r}.{ext}", f"surl-mcp-{r}.{ext}"], dl)
        unpack(dl / f"surl-{r}.{ext}", bin_ / "surl")
        unpack(dl / f"surl-mcp-{r}.{ext}", bin_ / "surl-mcp")
        how["surl"] = f"release {tag}"
    else:
        build(src["surl"] / "Application/Surl.Console", bin_ / "surl")
        build(src["surl"] / "Application/Surl.Mcp", bin_ / "surl-mcp")
        how["surl"] = "source"

    # Curl: curl, and curl-mcp from its tool package
    tag = None if a.source_only else release_at(REPOS["curl"], pin["curl"])
    if tag:
        ext = "zip" if r.startswith("win") else "tar.gz"
        dl = out / "download" / "curl"
        got = download(REPOS["curl"], tag, [f"curl-{r}.{ext}", f"Surly.Curl.Mcp.{r}.*.nupkg"], dl)
        unpack(dl / f"curl-{r}.{ext}", bin_ / "curl")
        mcp = next(f for f in got if f.name.startswith(f"Surly.Curl.Mcp.{r}."))
        unpack(mcp, bin_ / "curl-mcp", f"tools/any/{r}/")
        how["curl"] = f"release {tag}"
    else:
        build(src["curl"] / "Curl.Console", bin_ / "curl")
        build(src["curl"] / "Curl.Mcp", bin_ / "curl-mcp")
        how["curl"] = "source"

    # The libraries, always from source, at versions the recipes' @* references select.
    for project in (src["surl"] / "Application/Surl.Api", src["curl"] / "Curl.Api", src["curl"] / "Libcurl"):
        pack(project, feed)

    curl_mcp = next((p for p in (bin_ / "curl-mcp" / f"Curl.Mcp{EXE}", bin_ / "curl-mcp" / f"curl-mcp{EXE}") if p.exists()), None)
    if not curl_mcp:
        sys.exit("no Curl.Mcp or curl-mcp binary in bin/curl-mcp")
    values = {
        "SURLY_SURL": bin_ / "surl" / f"surl{EXE}",
        "SURLY_CURL": bin_ / "curl" / f"curl{EXE}",
        "SURLY_SURL_MCP": bin_ / "surl-mcp" / f"surl-mcp{EXE}",
        "SURLY_CURL_MCP": curl_mcp,
        "SURLY_NUGET_SOURCE": feed,
        "NUGET_PACKAGES": out / "nuget",   # fresh: Surly.* package versions repeat across pins
    }
    for k, v in values.items():
        if k.startswith("SURLY_") and k != "SURLY_NUGET_SOURCE" and not Path(v).exists():
            sys.exit(f"{k}: {v} is missing")
    posix = lambda p: Path(p).as_posix()
    # PATH entries are colon-separated, so on Windows a drive letter is written Git Bash's way: Z:/x is /z/x.
    shell = lambda p: re.sub(r"^([A-Za-z]):", lambda m: "/" + m.group(1).lower(), posix(p))
    lines = [f"export {k}={posix(v)!r}" for k, v in values.items()]
    lines.append(f"export PATH={shell(bin_ / 'surl')!r}:{shell(bin_ / 'curl')!r}:\"$PATH\"")
    (out / "env.sh").write_text(
        f"# Written by build/fetch_pins.py: Surl {pin['surl'][:9]} ({how['surl']}), Curl {pin['curl'][:10]} ({how['curl']}), {r}.\n"
        + "\n".join(lines) + "\n", encoding="utf-8")
    if a.github_env:
        with open(os.environ["GITHUB_ENV"], "a", encoding="utf-8") as env:
            for k, v in values.items():
                env.write(f"{k}={v}\n")
        with open(os.environ["GITHUB_PATH"], "a", encoding="utf-8") as path:
            path.write(f"{bin_ / 'surl'}\n{bin_ / 'curl'}\n")
    print(f"Surl {pin['surl'][:9]}: {how['surl']}; Curl {pin['curl'][:10]}: {how['curl']}; {r}; env in {out / 'env.sh'}")


if __name__ == "__main__":
    main()
