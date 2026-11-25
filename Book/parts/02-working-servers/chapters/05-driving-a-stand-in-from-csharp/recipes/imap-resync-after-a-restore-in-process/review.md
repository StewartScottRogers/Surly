<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-resync-after-a-restore-in-process
Verdict: drafted, ready for review
Checked against local builds on 2026-11-25 (not releases): surl 1.0.0 (Surl v0.1.8, 9f8005dff), Curl port (Curl 2145a49eae), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet.
2. UIDVALIDITY values are not claimed, only that they differ (regex lookahead); RestoreAsync changes it even with nothing changed, ResetAsync too.
3. The UID SEARCH answer for a range above the largest UID (the largest) is cited to RFC 9051 and facts/mail.md, not a case here.
