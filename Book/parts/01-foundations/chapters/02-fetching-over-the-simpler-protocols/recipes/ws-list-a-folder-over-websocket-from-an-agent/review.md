<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-list-a-folder-over-websocket-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. The listing-off case adds the server's own 404 from get_exchanges, since the exit code 22 alone is ambiguous.
2. transfer on a ws URL answers statusCode 101, the status of the upgrade.
3. Bounded wait before reading the server's record (CI race sweep).
