<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-exchange-a-password-for-a-token-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.

## Findings

1. Same nearest-honest shape as the console recipe.
2. Uses curl-mcp's curl tool, since transfer has no --cacert.
