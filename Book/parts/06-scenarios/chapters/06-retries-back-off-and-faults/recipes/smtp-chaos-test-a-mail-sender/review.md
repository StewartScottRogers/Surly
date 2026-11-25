<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-chaos-test-a-mail-sender
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet.
2. The duplicate case measures the cut point from a clean exchange (bytes sent up to the 354, plus one) rather than hard-coding it; drop with after-bytes and close graceful.
3. list_faults keeps spent rules (remaining 0), so the case claims that rather than an empty list.
