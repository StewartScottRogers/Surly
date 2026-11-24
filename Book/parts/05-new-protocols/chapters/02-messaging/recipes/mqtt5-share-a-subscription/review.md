<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: mqtt5-share-a-subscription
Verdict: drafted, ready for review
Checked against local builds on 2026-11-24 (not releases): surl 1.0.0 (Surl v0.1.5, d64faa4bb), Curl port 8.22.0 (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 6 of 6 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case or cited.
2. Each curl takes about 3 s to start, connect and publish on Windows, so the first run overran the 30 s case limit after three jobs. The solution case now has timeout: 150 and the subscribers -m 120; the script gives up after 40 pings rather than claiming 20 s. Which worker gets which job is left to the broker, so the claims are bounds (1 to 5 each) and each-once. Needs a POSIX shell.
