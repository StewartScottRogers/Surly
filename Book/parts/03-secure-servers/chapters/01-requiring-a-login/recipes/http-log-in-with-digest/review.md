<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-log-in-with-digest

Verdict: accept with fixes

Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 7 of 7 cases pass on windows.

## Findings

1. The challenge case matches the whole header block, so "Digest and no other method over http://" is proved, not only the three algorithms.
2. Truth (5): curl answers the first challenge, MD5, and has no option to pick another, so SHA-256 and SHA-512-256 are proved offered, not proved checked. The Discussion says the recipe shows only MD5 and cites the README for the other two.
3. `-v ... 2>&1` relies on curl's verbose header line format; a reasonable thing for a port to keep, but a fragile case.
4. Acronyms: MD5 and SHA spelled out.
