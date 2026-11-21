<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-check-unread-counts
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. Delivery is asynchronous, so the seed waits for message 3 with `curl --retry` on `FETCH 3 FLAGS` (exit 21 while it is missing), bounded at ten tries, rather than a fixed sleep; the 21 is its own case.
2. curl fetches with `BODY[...]`, not `BODY.PEEK[...]`, so even a header-only fetch marks a message read; stated and shown with -v.
3. surl is IMAP4rev1: `STATUS (SIZE)` is BAD; recorded as a case and in facts/mail.md.
