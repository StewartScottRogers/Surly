<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-refuse-a-password-in-clear

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 10 of 10 cases pass on windows.

## Findings

1. Three servers: the default (`main`), the loosened one (`loose`, `--allow-plaintext-auth`) and HTTPS (`tls`, certificate from `setup:`). The refusal, its "unchecked" nature (wrong password, same 403), the loosening and the TLS path each have a right- and wrong-password case.
2. "The default 401 offers nothing but Digest" matches the whole header block; the loosened one's Basic and Bearer challenges are matched in order after the Digest ones.
3. The Solution's output is a refusal, `403`, which fits the Problem (checking that a client is refused); the ways in follow in the Discussion.
4. The startup warning for `--allow-plaintext-auth` is cited to the README: verify.py cannot see surl's log.
5. Re-pinned to Surl a423807a5d / Curl 0680b97577: the 401 now carries Content-Type and a 17-byte body (BL-1444) before its WWW-Authenticate lines.
