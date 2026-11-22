<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-read-a-server-stream-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 5 of 5 cases pass on windows.

## Findings

1. Same five claims; curl-mcp keeps the replies of a failed stream in standardOutput.
