<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-upgrade-to-http2
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b): 8 of 8 cases pass on windows.
## Findings
1. Fixed: the Solution first claimed the whole `-i` output with a regular expression, which the book printed as a pattern instead of output; the Solution now prints the body and `HTTP/2`, and the `101 Switching Protocols` then `HTTP/2 200` sequence is its own case (`shows-the-switch`).
2. Fixed: cut "fails outright against a server that does not speak HTTP/2" (prior knowledge) and "stream 1" (the answer's stream number); neither had a case. The deprecation of the `h2c` upgrade is a citation of RFC 9113 section 3.1, not a program claim.
3. Truth (4): `body-ignores-the-upgrade` checks only the version (`1.1`). The status of that request is 401 with a Digest challenge, though `surl --aihelp http` says POST is answered 405 with no accounts; the recipe does not claim the status, and the lead has it as a program finding.
4. Contrast with `--http2-prior-knowledge` (Part 1, `http-choose-the-http-version`, which links here) is a case: no 101, the answer starts `HTTP/2 200`.
5. Reader: acronyms HTTP/2, TLS spelled out; the hand-made upgrade with `!!!` for `HTTP2-Settings` is explained where it is used.
