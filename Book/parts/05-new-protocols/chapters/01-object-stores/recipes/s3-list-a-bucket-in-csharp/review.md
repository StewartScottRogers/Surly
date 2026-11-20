<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-list-a-bucket-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 8 of 8 cases pass on windows.

## Findings

1. All console claims carried. Surl.Api `SelfSigned` cannot be used: its certificate does not name the bucket host (curl 60 even with `VerifyHost = false`), so `setup:` makes one with openssl.
