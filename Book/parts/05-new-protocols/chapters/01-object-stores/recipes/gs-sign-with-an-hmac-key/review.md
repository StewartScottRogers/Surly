<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: gs-sign-with-an-hmac-key

Verdict: accept

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.

## Findings

1. Decision: the Solution is two transfers joined with `--next`, one HMAC-signed and one with `--oauth2-bearer`, so one command shows both logins.
2. A token is `--user :TOKEN` (empty name); an unknown token is `401 InvalidAuthentication`, an unknown access key `403 InvalidAccessKeyId`, no login `401 AuthenticationRequired`. Uploads off: `403 AccessDenied` for both logins.
3. Left out: the Cloud Storage JSON API (Bearer only, an HMAC signature there is 400) and `--gs-plaintext`.
