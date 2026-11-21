<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-multiplex-requests-over-http2-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. `transfer` has no version field, so the `curl` tool carries `-Z`; it accepts URL globs and `-o got/f#1.txt --create-dirs`.
2. Server-side proof is `list_servers` `exchange_count` (surl-mcp has no readiness probe); the HTTP/1.1 case checks the count equals the transfers that opened a connection.
3. Timing is a Stopwatch around the tool call; ranges 2-4 s and 10 s or more.
