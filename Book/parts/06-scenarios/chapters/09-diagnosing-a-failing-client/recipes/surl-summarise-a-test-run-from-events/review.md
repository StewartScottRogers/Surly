<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-summarise-a-test-run-from-events
Verdict: accept with fixes
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
4 of 4 cases pass on Windows.
1. Counts are exact: logins are judged per request, so the retried fetch on one connection counts twice (5 accepted). If a platform closes the connection after the 503, that count could change; watch CI.
2. Shows the gap honestly: an FTP password refused unread (no `--allow-plaintext-auth`) leaves no login_judged event.
