<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-a-file-in-parallel-ranges

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 5 of 5 cases pass.

## Findings

1. --parallel-immediate is needed for the four transfers to open four connections; without it the second range reused a connection every time, even against a 1 s delay server.
2. Solution's num_connects column is matched as 0 or 1 on the fast server; the slow server (--fault delay) case shows 1 for all four.
3. CI fix (run 44871788391): BSD `wc -c` on macOS pads its count (`joined:  1000000`); `check.sh` now prints `$(wc -c < joined | tr -d " ")`. Regexes and exact outputs unchanged.
