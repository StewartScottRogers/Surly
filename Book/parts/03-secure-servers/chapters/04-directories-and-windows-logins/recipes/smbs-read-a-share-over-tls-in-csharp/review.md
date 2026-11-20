<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smbs-read-a-share-over-tls-in-csharp

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.

## Findings

1. Covers the trusted read, 60, 67, the start refusal and SelfSigned with and without VerifyPeer.
