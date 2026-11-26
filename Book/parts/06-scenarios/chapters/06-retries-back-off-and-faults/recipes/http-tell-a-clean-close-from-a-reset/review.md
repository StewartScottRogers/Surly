<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-a-clean-close-from-a-reset
Verdict: drafted, ready for review
Checked against local builds on 2026-11-26 (not releases): Surl master 1e5204d, Curl master f2331ebe (surl, curl): 5 of 5 cases pass on windows, three runs.
## Findings
1. Self-review against REVIEW.md; no independent reviewer yet. Every Discussion claim is a case; the macOS note is platforms.md (Curl BL-5799).
2. Graceful cases are exact (213-byte head, 999,787 body bytes, 1000213 missing) and unmarked, so they run on macOS in CI for the first time; reset cases claim only bounds and are [windows, linux].
3. The cut lines are verbose-level only; at the default level both endings log the same IOException line, so the server runs with -v --log-file.
