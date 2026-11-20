<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-catch-a-seed-the-server-refuses

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.

## Findings

1. A client's PUT to /.surl/seed.txt is now 403 Forbidden (was 409 Conflict, reported and fixed).
2. The SMTP peer side is not shown: an SMTP client login needs TLS or --allow-plaintext-auth, which would add a loosening the task does not need.
3. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: surl refuses an upload into .surl before checking its folder exists, so the client's PUT is 403, not 409; cases and Discussion updated.
