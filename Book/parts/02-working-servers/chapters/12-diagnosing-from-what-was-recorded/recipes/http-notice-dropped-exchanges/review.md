<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-notice-dropped-exchanges
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, Libcurl 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Each request carries Connection: close so one request is one exchange; eight workers keep the run near 10 s. The oldest kept item varies with scheduling and is claimed as a range.
3. Exit 2 is the test's own 'cannot tell' result.
