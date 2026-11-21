<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-find-who-failed-to-log-in-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.

## Findings

1. A wrong password and an unknown account give the same event shape under the claimed name; the Discussion says the server does not tell them apart.
2. The client address is read from the exchange_started event; its port varies, so the case matches it with a pattern.
