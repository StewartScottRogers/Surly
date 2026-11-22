<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-search-by-sender-and-date-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 12 of 12 cases pass on windows.
## Findings
1. Messages seeded with seed_mail carry their own Date headers; each is waited for by its store_changed event.
2. Same twelve claims as the console recipe; the BAD line is read from curl -v.
