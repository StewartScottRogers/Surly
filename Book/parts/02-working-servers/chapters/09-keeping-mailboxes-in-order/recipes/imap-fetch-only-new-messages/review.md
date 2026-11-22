<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-fetch-only-new-messages
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 10 of 10 cases pass on windows.
## Findings
1. curl's URL query (INBOX?UID%203:*) sends plain SEARCH, so it answers message numbers; the recipe uses -X "UID SEARCH ..." and shows the two diverge after an EXPUNGE.
2. UID SEARCH UID n:* with n above the largest UID answers the largest (RFC 9051 behaviour); claimed, the job must filter.
