<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-time-out-a-slow-call

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 8 of 8 cases pass on windows.

## Findings

1. -m is sent as grpc-timeout (second surl with -v --log-file names it for the call with -m only); the race outcome without -H is cited from platforms.md, not a case, since it differs by platform.
2. Timings are ranges (1 to 3 s).
- Lead's batch verify: time_total was 0.998 with -m 1; the claim now allows 0.9 to 2.9 s.
