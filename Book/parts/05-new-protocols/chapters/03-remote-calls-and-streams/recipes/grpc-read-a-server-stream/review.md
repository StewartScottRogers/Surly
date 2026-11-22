<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-read-a-server-stream

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 5 of 5 cases pass on windows.

## Findings

1. count is limited to 1000 (1001 is INVALID_ARGUMENT); delayMilliseconds is applied before each reply, so replies arrive spaced, not at the end.
2. The in-flight claim is a range (some, not all of ten) so it holds on a loaded runner.
