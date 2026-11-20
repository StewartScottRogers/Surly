# ADR-0008: Recipes from code and agents: C# scripts and MCP cases

## Status

Accepted, 2026-11-18. Extends ADR-0005 (item 6, the expected addition of cases that run a program rather than a shell line). Introduces `recipe.yml` schema 2.

## Context

Schema 1 knows one kind of case: a shell line, judged by exit code, standard output, standard error and files on disk. That covers `surl` and `curl` on the console, which stays the book's default and its first subject.

Both programs have two more surfaces that readers will use:

| Surface | Surl (server) | Curl (client) |
| --- | --- | --- |
| Console | `surl` | `curl` |
| .NET library | `Surl.Api`: `SurlServer.StartAsync` runs a server in-process; its options are `surl`'s options by name | `Curl.Api`: an async, `HttpClient`-shaped client; `Libcurl`: the libcurl API one call for one call, for porting C code |
| MCP server (stdio) | `surl-mcp` (`Surl.Mcp`): start, list and stop servers; seed stores; read back exchanges, events and what a client left | `curl-mcp` (`Curl.Mcp`): the `curl`, `transfer`, `parse_url`, `build_url` and `version` tools |

The author wants these documented and tested the same way the console is: every claim a case, every case run. For the library, the moral equivalent of the console's one-line command is a C# script: one source file, no project file, runnable on its own.

## Decision

1. **Library and MCP recipes are recipes of their own**, not variants of a console recipe. One recipe, one surface.
2. **They live in the difficulty tiers**, in chapters of their own inside each tier (for example `parts/01-foundations/chapters/<nn>-from-csharp/`), not in a separate part. Difficulty stays the book's one ordering (ADR-0004). A new metadata field, `surface`, says which surface a recipe uses: `console` (the default), `csharp` or `mcp`.

   **Amended 2026-11-19 by the author (with ADR-0009):** a triplet (one task written for the console, C# and an agent) sits together in its task's chapter, so a reader sees the three ways side by side. A C# or agent recipe with no console twin still goes in a chapter of its own surface.
3. **`Libcurl` recipes are `advanced` tier only.** `Curl.Api` and `Surl.Api` may appear in any tier their difficulty puts them in.
4. **Schema 2 adds two kinds of case.** Every case has exactly one of `run`, `program` or `mcp`; lint refuses a case with none or more than one. Schema 1 recipes stay valid.
5. **`program:` is a C# file-based app** (`dotnet run <file>.cs`, .NET 10 and later), kept beside `recipe.yml` and given as fixtures are:

   ```yaml
   - id: solution
     role: solution
     claim: Surl.Api serves a file that Curl.Api fetches.
     program: fetch.cs
     args: []
     expect:
       exit: 0
       stdout: "hello from surl\n"
   ```

   The script references the libraries with `#:package`, never `#:project`: the book tests what a reader can install. A script that hosts its server through `Surl.Api` needs no `servers:` block. The judging is schema 1's: exit code, output, files.
6. **Package versions are pinned once, not per script.** The pin lives in `Book/build/packages.yml`, and a release bumps it in one commit. Settled when the runner was built (2026-11-18): each script carries its real version (`#:package Surl.Api@1.0.0`) so a reader can copy it and run it as printed; `build/pin.py <package> <version>` rewrites `packages.yml` and every matching `#:package` line together, and lint refuses a line that differs from the pin. A central `Directory.Packages.props` and a `{{version}}` placeholder were both rejected because the script on disk would then not run as printed.
7. **`mcp:` is a list of tool calls** sent in order to `surl-mcp`, `curl-mcp` or both. verify starts each host as a stdio process in the recipe's temporary directory (`surl-mcp --root <dir>`, `CURL_MCP_ROOT=<dir>`) and performs the MCP `initialize` handshake. Settled when the runner was built (2026-11-18): each host is started once per recipe, not per case, and the recipe's cases run in order through it, so the solution can start a server and every later claim can use it, as a `servers:` block is shared by schema 1 cases. Argument names are the tools' own, as their input schemas give them:

   ```yaml
   - id: solution
     role: solution
     claim: An agent serves a file with surl-mcp and fetches it with curl-mcp.
     mcp:
       - host: surl
         tool: start_server
         arguments: { listen_urls: ["http://127.0.0.1:0"], files: { hello.txt: "hello from surl\n" } }
         save: { url: bound_urls.0, server: server_id }   # a dotted path into the answer; later calls use {{url}}
       - host: curl
         tool: transfer
         arguments: { url: "{{url}}hello.txt" }
         expect:
           result-contains: { statusCode: 200, body: { content: "hello from surl\n" } }
   ```

   A call's `expect` may hold `result-contains` (a subset of the answer: objects by key, lists by any matching item), `is-error: true` (a refusal, with optional `error-contains`), or nothing (the call need only not be an error). An `mcp:` case has no `expect` of its own; it passes when every call's expectation holds. The answer is the tool's `structuredContent` when the host gives one, else its text parsed as JSON (`curl-mcp` gives only text). Settled 2026-11-18 with the author: the client is the official C# MCP SDK (`build/McpBridge.cs`), not one the book wrote, and every call's arguments are checked against the tool's input schema from `tools/list` before it is sent, so a renamed or retyped argument fails by name.

   **Amended 2026-11-19 by the author: MCP recipes are C# programs.** The `mcp:` call list was notation only the book could run. An `mcp` recipe's cases are now `program:` cases, like a `csharp` recipe's: a C# file-based app on the official MCP SDK (`#:package ModelContextProtocol.Core`, pinned in `packages.yml`) that starts `surl-mcp` and `curl-mcp` by name from PATH over stdio, confines them with `SURL_MCP_ROOT` and `CURL_MCP_ROOT`, calls their tools as an agent would, and prints what the claims check. A reader can run it as printed. verify puts the hosts on PATH for these programs and records the versions they report. The `mcp:` form, the bridge and the pre-call schema check are retired with it: a renamed argument now shows as a tool error inside the program. `surl-mcp`'s `get_exchanges` and `wait_for_event` let a Discussion claim what a client sent on the wire, not only what it printed.
8. **Metadata grows to match.** Beside `curl-options` and `surl-options`: `api-types` (the public types a `csharp` recipe uses) and `mcp-tools` (the tools an `mcp` recipe calls). Lint requires every listed type or tool to be explained in the Discussion, as it does for options.
9. **verify records every version it ran against**: the .NET SDK, each package from item 6, and each MCP host (the version it reports in the `initialize` handshake, since `curl-mcp` has no `--version`), beside the `surl` and `curl` versions in `verified.yml`. The MCP hosts are found on the path or named by `SURLY_SURL_MCP` and `SURLY_CURL_MCP`, as `SURLY_SURL` and `SURLY_CURL` name the binaries.
10. **The build prints what was run.** For `program:` it prints the C# source and the `dotnet run` line; for `mcp:` it prints each call, what it saves and what it must answer, as JSON. `run.sh` and `run.ps1` call `dotnet run` for a `csharp` recipe; an `mcp` recipe gets `mcp.json`, an MCP client configuration listing the two hosts, instead.

## Consequences

- The book documents and tests all three surfaces of both programs from one method; a library or MCP regression breaks a recipe exactly as a console regression does.
- Every recipe is still judged by output a machine can compare, so `REVIEW.md`, lint and the dark factory need no new kind of judgement.
- verify gains two runners: `program:` (about half a day: `dotnet run`, version capture, listings) and `mcp:` (about a day: a small stdio MCP client in Python, saved values, JSON subset matching).
- verify now needs the .NET SDK on every platform it runs on, and package feeds reachable or cached.
- Until `Surl.Api`, `Curl.Api`, `Surl.Mcp` and `Curl.Mcp` are published as packages, `csharp` and `mcp` recipes stay `draft`, as console recipes do until the binaries are released.
- `Book/AI.md`, the recipe template (a `csharp` and an `mcp` variant beside the console one), `build/recipes.py`, lint, verify and build change when this is accepted, not before.
