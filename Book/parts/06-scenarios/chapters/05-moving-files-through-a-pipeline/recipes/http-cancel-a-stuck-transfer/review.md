<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-cancel-a-stuck-transfer

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 3 of 3 cases pass.

## Findings

1. Program finding (observed, maybe intended): after the client cancels, surl keeps counting a delay-faulted exchange until its delay ends (no ExchangeEnded within 10 s), so with MaxConnectionsPerAddress 1 the next request is 503. Claimed as observed.
2. Times stated as a 1 to 5 s range after a warm-up request.
