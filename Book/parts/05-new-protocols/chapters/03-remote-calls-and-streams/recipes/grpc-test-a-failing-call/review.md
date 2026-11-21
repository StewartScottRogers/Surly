<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-test-a-failing-call
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 9 of 9 cases pass on windows.
## Findings
1. Overlap with grpc-call-a-method (one status case). This recipe adds the range of statuses, the empty message, 0 as OK, percent in the message, a status number out of range (`INVALID_ARGUMENT`, request field out of range), the failure part way through a stream, and the client's own `-m` timeout (exit 28) against a server-chosen DEADLINE_EXCEEDED.
2. Unicode in `statusMessage` round-trips (probed with `café`); not cased, since a non-ASCII command line is a Windows risk.
3. `client-gives-up` now adds `-H "grpc-timeout:"`: `-m` is also sent as `grpc-timeout`, which surl enforces, so the server could answer DEADLINE_EXCEEDED (exit 22) before curl's 28, as the agent twin did on macOS in CI run 44835381862.
