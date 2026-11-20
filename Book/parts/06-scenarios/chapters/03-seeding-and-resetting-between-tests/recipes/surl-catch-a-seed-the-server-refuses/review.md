<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-catch-a-seed-the-server-refuses

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.

## Findings

1. A client's PUT to /.surl/seed.txt is 409 Conflict; 403 or 404 would match the content topic's 'refused' better. Reported, recipe states what happens.
2. The SMTP peer side is not shown: an SMTP client login needs TLS or --allow-plaintext-auth, which would add a loosening the task does not need.
