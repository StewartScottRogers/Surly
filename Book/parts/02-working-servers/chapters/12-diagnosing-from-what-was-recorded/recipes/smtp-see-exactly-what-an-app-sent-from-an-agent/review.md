<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-see-exactly-what-an-app-sent-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. surl-mcp has no log, so the **** claim is cited to the console sibling; get_exchanges keeps the CRAM-MD5 answer raw (a case).
3. curl-mcp's MAIL FROM carries SIZE=117, the CRLF fixture's size.
