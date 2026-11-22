<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-time-out-a-slow-call-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 6 of 6 cases pass on windows.

## Findings

1. surl-mcp refuses --log-file, so the header cannot be read from a log; the claim is the either-timer-ends-it outcome, accepting exit 28 or 22.
