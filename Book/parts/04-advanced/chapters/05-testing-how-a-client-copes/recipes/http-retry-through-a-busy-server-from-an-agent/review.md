<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-retry-through-a-busy-server-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 4 cases pass on windows.
## Findings
1. Warm-up transfer to /health.txt before the clock; the timing claim is a range. Request counts come from the received chunks of get_exchanges.
2. surl-mcp and curl-mcp are not on PATH outside verify, so the program only runs under verify.
Re-pinned to Surl a423807a5d / Curl 0680b97577: each 503 now carries the body 503 Service Unavailable in standardOutput (BL-1444).
3. Bounded wait before reading the server's record (CI race sweep).
