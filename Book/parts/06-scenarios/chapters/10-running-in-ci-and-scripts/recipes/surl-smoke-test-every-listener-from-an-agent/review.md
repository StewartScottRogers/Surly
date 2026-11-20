<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-smoke-test-every-listener-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. -o smoke.out instead of /dev/null: curl-mcp confines output paths to CURL_MCP_ROOT.
2. get_events exchange_started is the server-side check (no readiness probe through surl-mcp, so exactly one per listener).
