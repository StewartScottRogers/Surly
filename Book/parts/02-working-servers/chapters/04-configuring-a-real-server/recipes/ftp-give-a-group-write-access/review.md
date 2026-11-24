<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-give-a-group-write-access
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 15 of 15 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. --allow-anonymous accepts every login unchecked as anonymous, so Ed finds the share missing there too; the anonymous caller comes from a second server (open), and strict shows the refused clear password.
3. open and strict share the same folder with ;readonly, which takes no lock, so they run beside main.
