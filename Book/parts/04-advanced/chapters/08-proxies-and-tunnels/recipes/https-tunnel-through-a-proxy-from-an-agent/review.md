<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-tunnel-through-a-proxy-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.

## Findings

1. Seven of the console's nine claims: the CONNECT and tunnel are read from get_exchanges (start_server refuses --log-file), so the two log-line claims become one exchange claim; curl -v has no MCP twin.
2. transfer's caFile is the origin's only: through the https proxy it fails 60; the curl tool with --proxy-cacert succeeds.
