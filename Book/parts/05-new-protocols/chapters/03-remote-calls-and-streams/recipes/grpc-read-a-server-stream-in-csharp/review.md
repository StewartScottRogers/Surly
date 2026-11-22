<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-read-a-server-stream-in-csharp

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl commit 5da5b3eaf, Curl commit c84035f797: 5 of 5 cases pass on windows.

## Findings

1. Finding: Curl.Api throws CurlException 22 for a stream that ends in a non-OK status and does not hand over the replies sent before it; the console prints them. The case states this.
2. The response Body fills as replies arrive (a read cancelled after 1 s has some, not all).
