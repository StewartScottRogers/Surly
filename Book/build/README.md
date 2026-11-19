# Build, verify, lint

## Once, on any machine

```
npm install -g asciidoctor          # the reference renderer (Node)
pip install pyyaml                  # the scripts' one dependency
```

Portable Document Format and EPUB need the Ruby renderers (`gem install asciidoctor-pdf asciidoctor-epub3`); the build adds them once they are decided.

## Every time

```
python3 build/lint.py     # shape: sections, anchors, copyright, options explained, references resolve
python3 build/verify.py   # truth: runs every case against surl and curl, writes verified.yml per recipe
python3 build/build.py    # the book
python3 build/site.py     # the website into out/site/: cover, a searchable folded contents, one page per recipe (claims and references folded), the whole book folded by part, chapter and recipe, all in site/surly.css and surly.js; the pitch into dist/ (cover PNG and PDF need Node with playwright, skipped without it)
```

`verify.py` finds `surl`, `curl`, `dotnet`, `surl-mcp` and `curl-mcp` on PATH, or takes `SURLY_SURL`, `SURLY_CURL`, `SURLY_DOTNET`, `SURLY_SURL_MCP` and `SURLY_CURL_MCP`; it needs only the ones the selected recipes use, and records only their versions. It accepts recipe keys to run a subset. A recipe stays `draft` until it has been verified against released binaries and packages; the book says so under each Solution.

`build/recipes.py` is the shared definition of a recipe (folders, `recipe.yml`, placeholders, how a case's command is spelled) that all three scripts import.

`setup:` (schema 2) is a list of command lines run in the recipe's working directory after the fixtures are written and before any server starts: certificates with `openssl`, SSH keys with `ssh-keygen`. Each must exit 0 and none is a claim; the book prints them first in the Solution, and `run.sh`/`run.ps1` run them too. Keys and certificates are made this way, never committed as fixtures: they would expire and they would be private keys in a public repository. `Book/.gitignore` keeps the ones a reader's `run.sh` makes out of git.

Placeholders: `{{port}}` and `{{port2}}` to `{{port9}}` are each a free port at run time, shown in the book as 8080 to 8088, so one recipe can run several listeners, or two servers side by side (a default one and a loosened one). A server with `ready: port` is waited for on every port its command names; `ready: none` for a UDP listener such as TFTP. `{{null}}` is `/dev/null`, or `NUL` on Windows.

## C# programs (schema 2, ADR-0008)

A `program:` case is a C# file-based app beside `recipe.yml`. verify builds it once with `dotnet build <file>.cs`, then runs each case with `dotnet run --no-build <file>.cs -- <args>` in the recipe's temporary directory, so stdout is the program's alone. It needs the .NET 10 SDK or later.

```
python3 build/pin.py Surl.Api 1.0.0     # pin a package for the whole book: packages.yml and every #:package line
SURLY_NUGET_SOURCE=/path/to/feed python3 build/verify.py <key>   # add a local folder or feed, for packages not yet published
```

`build/packages.yml` is the one place package versions are pinned. Every `#:package` line must name its package at that version (lint checks it), so a reader can copy a program and run it as printed. `#:project` is refused: the book tests what a reader can install. NuGet caches a package by version, so after repacking the same version into a local feed, delete it from `~/.nuget/packages/` before verifying again.

## MCP recipes (schema 2, ADR-0008 as amended)

An `mcp` recipe is written like a `csharp` one: each case is a `program:`, a C# file-based app on the official MCP SDK (`ModelContextProtocol.Core`, pinned in `packages.yml`). The program starts `surl-mcp` and `curl-mcp` by name with `StdioClientTransport`, confines them with the environment variables `SURL_MCP_ROOT` and `CURL_MCP_ROOT` set to its working directory, calls their tools, and prints what the claims check. verify finds the hosts on PATH or through `SURLY_SURL_MCP` and `SURLY_CURL_MCP`, and puts small `surl-mcp` and `curl-mcp` shims first on PATH for these programs only, so a host built under another name (`Curl.Mcp.exe`) is still found by name. It records each host's version from the MCP handshake, and the SDK's version from the program's `#:package` line. lint reads the tools a program calls from its `CallToolAsync("...")` calls and checks each is in `mcp-tools` and explained in the Discussion. The build writes `mcp.json`, a client configuration for the two hosts, beside `run.sh`.

Pass a host's root as an environment variable, never as an argument with a path in it: on Windows the SDK starts a host through `cmd.exe`, which breaks on a path with a space.

```
SURLY_SURL_MCP=/path/to/surl-mcp SURLY_CURL_MCP=/path/to/curl-mcp python3 build/verify.py <key>
```

Not committed: `generated/`, `out/`, and each recipe's `.generated/`. Committed: each recipe's `run.sh`, `run.ps1`, fixtures and `verified.yml`.

## Commit signing (once, the author)

Run these two commands once on the machine you commit from; they create a key tied to your GitHub email and tell git to sign every commit with it.

```
ssh-keygen -t ed25519 -C "Stewart.Rogers@pobox.com" -f ~/.ssh/surly-signing -N ""
git config --global gpg.format ssh && git config --global user.signingkey ~/.ssh/surly-signing.pub && git config --global commit.gpgsign true
```

Then add `~/.ssh/surly-signing.pub` to GitHub as a **signing key** (Settings > SSH and GPG keys > New SSH key > Key type: Signing Key) so GitHub shows commits as Verified. Tick the box in `../RIGHTS.md`.
