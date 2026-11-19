<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-put-and-delete-files-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 8 of 8 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, `files.cs`, with the scenario as its argument; every claim kept. Each scenario now starts its own server, so `read-back`, `overwrite` and `nothing-to-delete` put `/reports/a.txt` themselves rather than relying on the solution's state.
2. `read_file` also answers `modified_at`, which varies per run; `read-back` and `bytes-as-base64` match it with `stdout-matches`, and the Discussion now names it.
3. Resolved in Surl: `put_file` with a relative path (`reports/b.txt`) or a `..` segment (`/../x.txt`) was a JSON-RPC Internal error; it is now a tool error with a reason (`NotRooted`, `DotSegment`). Observed, not claimed. The message ends in .NET's `(Parameter 'requestPath')`, which is noise for an agent.
4. The client refusal is still shown with `-X DELETE -s -w "%{http_code}"` and no `-o`, because curl-mcp refuses the null device (known Curl finding); honest only because the 401 body is empty.
5. <<http-accept-uploads>> and <<http-reset-and-snapshot-from-an-agent>> are cross-references to Part 2 recipes.
