<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-find-a-slow-path-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. get_exchanges chunks carry no timestamps and exchange_bytes events came back empty, so timing is per exchange from exchange_started/exchange_ended; the one-connection case shows the limit. A per-request timing in the record would make this recipe simpler (suggestion, not a bug).
2. Ranges: 1.5 to 4 s for the held path, under 1 s for the others; curl-mcp warmed up on a second server.
