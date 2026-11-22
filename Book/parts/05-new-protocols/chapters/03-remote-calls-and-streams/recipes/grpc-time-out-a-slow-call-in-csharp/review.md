<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-time-out-a-slow-call-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 6 of 6 cases pass on windows.

## Findings

1. Finding: CurlRequest.Headers with an empty value sends grpc-timeout empty and the server answers INTERNAL malformed grpc-timeout; RemovedHeaders is the equivalent of -H name: .
2. Console cases m-is-sent, header-left-off and log-names-the-header are one case here.
