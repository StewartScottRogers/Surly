<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-show-the-response-headers

Verdict: accept

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 4 of 4 cases pass on windows.

## Findings

1. Machine (12): `Date` and `Last-Modified` are deliberately unclaimed, and the Discussion says why; the regular expressions skip any header lines between the status line and `Content-Length`.
