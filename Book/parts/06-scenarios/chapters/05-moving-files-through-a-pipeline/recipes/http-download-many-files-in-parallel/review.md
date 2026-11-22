<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-many-files-in-parallel

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 5 of 5 cases pass.

## Findings

1. Truth: a case that counted connections (at most eight with --parallel-max 8) was dropped: curl opened a ninth once, closing and reopening one, without a 503. The solution held across five runs; if CI ever sees a 503 there, lower --parallel-max below the limit.
2. Uses a fixture script (make-site.sh) in setup to make fifty random files; diff -r -x .surl compares the folders.
- CI (race sweep run, Linux): the transfers finished too fast to overlap, so none met the limit; the server now holds each request 300 ms (a delay fault), so they overlap on every platform.
- CI (macOS): with overlapping transfers, --parallel-max 8 against a limit of 8 sometimes met a connection not yet freed (one 503); the cap is now 6, and the prose says why.
