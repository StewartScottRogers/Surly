<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: signalr-stream-items-from-a-hub-from-an-agent

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.

## Findings
1. The 401 wording differs from the console: curl-mcp's stderr has `SignalR: The negotiate request failed with HTTP status 401.` (a `SignalR: ` prefix the console message lacks); the case matches the whole line.
2. A hub error is a tool error in curl-mcp, so the program exits 1 on those scenarios by design.
