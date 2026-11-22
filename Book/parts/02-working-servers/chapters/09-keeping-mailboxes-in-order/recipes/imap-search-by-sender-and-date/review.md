<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-search-by-sender-and-date
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 12 of 12 cases pass on windows.
## Findings
1. SINCE and BEFORE compare the arrival date, SENTSINCE and SENTBEFORE the Date header; curl's APPEND cannot set a date, so the dated cases use SENT* and only the far-past and far-future SINCE cases (no case depends on today's date).
2. A search answers message numbers; the last two cases remove message 1 and show UID SEARCH answering UIDs. A bad date is BAD Invalid arguments, exit 21; an empty match is `* SEARCH`, exit 0.
