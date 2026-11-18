<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-a-directory

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit 3c5b39474), Curl port 8.22.0 (Curl commit b140f6d88c): 4 of 4 cases pass on windows.

## Findings

1. Written before this review existed; reviewed now against REVIEW.md. All four claims are cases and pass.
2. Fixed in the tooling, not the recipe: verify.py wrote fixtures with CRLF on Windows, so `hello.txt` was 17 bytes there; the recipe passed anyway because curl's output is read as text. Fixtures are now byte-exact.
3. Truth (5): "secure by default" and the warning on every loosening are the Surl README's claims, cited, not cases here; `<<http-require-a-login>>` carries them as cases once written.
