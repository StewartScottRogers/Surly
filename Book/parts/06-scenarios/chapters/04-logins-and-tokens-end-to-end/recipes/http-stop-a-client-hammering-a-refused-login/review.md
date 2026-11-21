<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-stop-a-client-hammering-a-refused-login

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.

## Findings

1. The refusal is `--fault refuse-login;account=app`; the count of tries is read from the server's verbose log (`grep -c`), one server and log per client so counts do not mix.
2. `--retry-delay 1` is there only to keep the hammering case short (the Discussion says so).
