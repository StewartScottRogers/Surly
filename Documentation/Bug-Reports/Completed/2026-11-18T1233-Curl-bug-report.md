<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-18 12:33 (UTC-7)

From the *Surly* cookbook (`Z:\repos\Surly\Book`), found while writing and verifying Part 1. Each finding was reproduced again just before this report was written.

**Build tested:** Curl at commit `b140f6d88c` (2026-11-18 08:24), built `dotnet build Curl.Console -c Release` and `Curl.Mcp`; `Curl.Api` and `Libcurl` packed `-p:BundledProjectReferencesPrivateAssets=all`. The binary reports `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`, and `curl-mcp` reports serverInfo `Curl.Mcp 0.1.0.0`. Windows 11, .NET SDK 11.0.100. **Curl `master` has moved since** (now `0a0b20d479`, 2026-11-18 12:11), so check whether these still reproduce.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | `curl-mcp`, `curl` tool | `-o NUL` and `-o /dev/null` are refused as paths, so an agent cannot throw a body away |
| 2 | Low | `Curl.Api` README | The usings sample leaves out `Curl.Api.Codes`, which `CurlCode` needs |

---

## 1. `curl-mcp` refuses the null device for `-o`

**Repro.** Start curl-mcp with `CURL_MCP_ROOT` set to an existing folder, then call the `curl` tool against any HTTP server, here a Surl server holding `hello.txt`:
```json
{"name": "curl", "arguments": {"args": ["-s", "-o", "NUL", "-w", "%{http_code}", "http://127.0.0.1:<port>/hello.txt"]}}
{"name": "curl", "arguments": {"args": ["-s", "-o", "/dev/null", "-w", "%{http_code}", "http://127.0.0.1:<port>/hello.txt"]}}
```

**Observed:** both are tool errors, and curl never runs (`exitCode: null`):
- `NUL`: `failure.kind: "PathRefused"`, text `Refused -o: -o 'NUL' cannot be resolved: The parameter is incorrect. : '\\.\NUL'.`
- `/dev/null`: `failure.kind: "PathRefused"`, text `Refused -o: -o '/dev/null' resolves to '\dev\null', which is outside CURL_MCP_ROOT (...)`.
- `-o copy.txt`, a path inside the root, works: `exitCode: 0`, stdout `200`.

**Expected:** the null device is not a file inside or outside the root; it is the conventional way to discard a body. `curl -s -o /dev/null -w "%{http_code}" URL` is one of the most common curl command lines, and upstream curl accepts both spellings. The root confinement should treat `NUL` (Windows) and `/dev/null` (everywhere, as curl's own documentation spells it) as allowed, discard-only targets.

**Impact:** an agent asking only for a status code has to download the body to stdout or into a file in the root. The *Surly* MCP recipes work around this by using `-s -w` without `-o`, which only works when the body is empty.

## 2. `Curl.Api` README: the usings sample leaves out `Curl.Api.Codes`

The README's `<!-- sample: usings -->` block lists `System.Net`, `Curl.Api`, `Curl.Api.Errors` and `Curl.Api.Options.Http`. But `CurlCode`, which the README uses (for example, `CurlException` carries a `CurlCode` in `Code`), lives in `Curl.Api.Codes`. A reader who copies the samples and switches on `ex.Code` gets a compile error until they add `using Curl.Api.Codes;`. Either add it to the usings sample or note the namespace where `CurlCode` is introduced.
