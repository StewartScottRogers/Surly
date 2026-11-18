<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-choose-the-http-version

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 4 of 4 cases pass on windows.

## Findings

1. Fixed: a garbled sentence about `-I` in the Discussion.
2. Added `rfc9113` (HTTP/2) to the bibliography for the lower-case header and no-reason-phrase claims.
3. Truth (5): the HTTP/1.0 claim (answered with an HTTP/1.1 status line) is a case and is what RFC 9110 asks of a server; `Connection: close` is a case.
4. `<<http-upgrade-to-http2>>` and `<<https-serve-http2>>` point at Part 4 recipes not written yet.
