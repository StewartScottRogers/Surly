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
python3 build/site.py     # the landing page into out/site/ and the pitch into dist/ (needs Node with playwright): generates each recipe's blocks, run.sh, run.ps1, the spine, index.json, by-protocol, and renders out/
```

`verify.py` finds `surl`, `curl`, `dotnet`, `surl-mcp` and `curl-mcp` on PATH, or takes `SURLY_SURL`, `SURLY_CURL`, `SURLY_DOTNET`, `SURLY_SURL_MCP` and `SURLY_CURL_MCP`; it needs only the ones the selected recipes use, and records only their versions. It accepts recipe keys to run a subset. A recipe stays `draft` until it has been verified against released binaries and packages; the book says so under each Solution.

`build/recipes.py` is the shared definition of a recipe (folders, `recipe.yml`, placeholders, how a case's command is spelled) that all three scripts import.

Placeholders: `{{port}}` and `{{port2}}` to `{{port9}}` are each a free port at run time, shown in the book as 8080 to 8088, so one recipe can run several listeners, or two servers side by side (a default one and a loosened one). A server with `ready: port` is waited for on every port its command names; `ready: none` for a UDP listener such as TFTP. `{{null}}` is `/dev/null`, or `NUL` on Windows.

## C# programs (schema 2, ADR-0008)

A `program:` case is a C# file-based app beside `recipe.yml`. verify builds it once with `dotnet build <file>.cs`, then runs each case with `dotnet run --no-build <file>.cs -- <args>` in the recipe's temporary directory, so stdout is the program's alone. It needs the .NET 10 SDK or later.

```
python3 build/pin.py Surl.Api 1.0.0     # pin a package for the whole book: packages.yml and every #:package line
SURLY_NUGET_SOURCE=/path/to/feed python3 build/verify.py <key>   # add a local folder or feed, for packages not yet published
```

`build/packages.yml` is the one place package versions are pinned. Every `#:package` line must name its package at that version (lint checks it), so a reader can copy a program and run it as printed. `#:project` is refused: the book tests what a reader can install. NuGet caches a package by version, so after repacking the same version into a local feed, delete it from `~/.nuget/packages/` before verifying again.

## MCP calls (schema 2, ADR-0008)

An `mcp:` case is a list of tool calls to `surl-mcp` and `curl-mcp`, found on PATH or named by `SURLY_SURL_MCP` and `SURLY_CURL_MCP`. verify starts each host the recipe calls once, confined to the recipe's temporary directory (`surl-mcp --root <dir>`, `CURL_MCP_ROOT=<dir>`), and runs the cases in order through the one pair of hosts, so a value saved by one call (`save: {url: bound_urls.0}`) is `{{url}}` in every later call. The client is `build/McpBridge.cs`, a C# file-based app on the official MCP SDK (`ModelContextProtocol.Core`). Neither host uses that SDK, so it is an independent check that they speak the protocol. verify builds it once per run, starts one bridge per host, and talks to it in one JSON line per call; `build/mcp.py` holds that side, plus the subset match behind `result-contains`. The bridge starts the host itself and gives its streams to the SDK, because the SDK's own stdio transport runs a host through `cmd.exe` on Windows, which breaks any path with a space in it. `SURLY_MCP_CLIENT=python` uses the book's original standard-library client instead, for a machine without .NET.

Before each call, verify checks the recipe's arguments against the tool's input schema from `tools/list`: a missing tool, a missing required argument, an argument the schema does not name, or a wrong type fails the case by name, before anything is sent. A call that expects `is-error: true` may break the schema on purpose; a missing tool never may. A tool's answer is its `structuredContent` when the host gives one, else its text parsed as JSON. `verified.yml` records each host's version from the handshake and the client (`mcp-client`).

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
