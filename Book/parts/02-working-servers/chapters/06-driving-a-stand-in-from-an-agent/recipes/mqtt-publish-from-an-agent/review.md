<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt-publish-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, `publish.cs`; every claim kept, one added. `empty-clears` now publishes its own retained message first.
2. Resolved in Surl (commit 993aa7072): a client PUBLISH without the RETAIN flag was kept as the retained message. It is no longer kept: `curl -s -d 22.0 mqtt://.../sensors/3` exits 0 and `read_retained_message` answers `{"retained":false}`. Now a claim, `client-publish-not-kept`, with `-d` in curl-options and the Discussion. No earlier claim changed.
3. The default server refuses curl's connection with no login (curl exit 8); the loosening, `--allow-anonymous`, is its own server, with its warning printed from `log_lines`.
4. The subscriber's output is printed as the JSON string curl-mcp answers, `"\u0000\tsensors/121.5"`, so the two length bytes show. Not claimed: that curl exits after the first message.
