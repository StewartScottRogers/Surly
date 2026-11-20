<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-a-fixture-from-a-config-file
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 7 of 7 cases pass on windows.
## Findings
1. test-env.conf holds the url lines with port 0, so check-env.sh reads the ports from the Listening on lines, as in surl-hand-bound-ports-to-the-app.
2. Fixed in the re-pin: a `#` comment after a valueless flag is now read as a comment. test-env.conf puts its comments after the two flags, so the solution case shows it; case no-comment-after-a-flag is gone.
3. Loosening options (--allow-plaintext-auth, --allow-uploads) each have a case without them, through options after -K (--no-allow-plaintext-auth: HTTP 403, curl 22; --no-allow-uploads: FTP 550, curl 25).
4. The FTP upload writes site/upload.txt and the mail store writes site/.surl/ in the reader's checkout when run.sh is run; harmless, not claimed.
5. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: no-comment-after-a-flag timed out because surl now reads `allow-uploads  # ...` as a flag and a comment, starts, and listens until killed; the claim is now that such a comment is ignored, shown by the solution.
