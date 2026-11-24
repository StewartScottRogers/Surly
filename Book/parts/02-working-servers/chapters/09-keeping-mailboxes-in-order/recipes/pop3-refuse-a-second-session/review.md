<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-refuse-a-second-session
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. The first session is held open with curl --limit-rate 100 on a ~1 KB message; two-clients.sh waits on the server log's Maildrop opened count (bounded 10 s) before the second login, so no race. Needs a POSIX shell. Cites new bibliography key rfc2449 for [IN-USE].
