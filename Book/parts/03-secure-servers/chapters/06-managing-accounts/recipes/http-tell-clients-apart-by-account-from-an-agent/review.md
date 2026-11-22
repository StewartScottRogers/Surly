<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-clients-apart-by-account-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.

## Findings

1. No surl-mcp tool returns the log, so the agent version reads events and exchanges only.
2. get_exchanges has no exchange_id filter; after_exchange_id N-1 with max_exchanges 1 picks one.
3. Bounded wait before reading the server's record (CI race sweep).
