<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-require-a-proxy-login-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 9 of 9 cases pass on windows.

## Findings

1. Carries the console claims. transfer has no proxy-login field; user:password in its proxy URL goes as Basic (403 over http), so Digest logins use the curl tool.
2. get_events login_judged with listen_url separates the proxy's and the origin's logins.
