<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-resend-after-a-dropped-connection
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. Every Discussion claim is a case or cited (rfc5321 for the reply code).
2. An accept-stage drop with times is spent by verify's readiness connection, so the servers use ready: none and a seed line waits for the IMAP port (listed last); on a slow runner the wait is bounded by 30 retries.
3. The exit 56 and "000 0" hold on Windows; the wording of the 56 message is the library's, so only `curl: (56)` is matched. Linux and macOS are not yet seen.
4. CI run 44898744862: Linux and macOS exit 7 (Could not connect), not 56, for the accept-stage drop; case drop-is-exit-56 became drop-fails-without-retry, which greps the -w line and the curl: (7|56) line from 2>&1, and the Discussion states 7 or 56 by platform and timing (facts/platforms.md).
