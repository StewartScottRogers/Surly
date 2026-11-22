<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-reset-and-snapshot-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, `reset.cs`; every claim kept. Every scenario starts the server, puts `fixture.txt` and takes the snapshot before diverging.
2. `exchanges-stay` repeats the solution's and the reset's four requests in one run, so `exchange_count` 4 still counts exactly those.
3. Snapshot and server IDs stay opaque: the program prints "answered the IDs it was given" after checking them, and `newest-four` matches the IDs in the error with `\S+`.
4. The `--directory` refusal is a second server in the same run, fixture `site/hello.txt`; the exact refusal text is now matched in full.
5. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: restore_server's refusal now gives both reasons a snapshot is missing (never taken of this server, or dropped past the newest 4); case and Discussion quote it.
6. Bounded wait before reading the server's record (CI race sweep).
