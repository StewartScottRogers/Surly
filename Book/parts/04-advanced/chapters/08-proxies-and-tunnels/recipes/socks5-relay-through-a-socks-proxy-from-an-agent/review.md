<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: socks5-relay-through-a-socks-proxy-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 9 of 9 cases pass on windows.

## Findings

1. Carries the console claims, read from get_exchanges' raw SOCKS bytes (address type 03 name vs 01 address, replies 05 00/04/02, 00 5B, 05 FF, 01 01) instead of the log.
2. transfer takes socks schemes and user:password@ in proxy; it has no --resolve, so the two resolve cases use the curl tool.
