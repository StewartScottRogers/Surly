<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-verify-a-download-checksum-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 4 of 4 cases pass on windows.

## Findings

1. `IncrementalHash` is a .NET type, not Surl/Curl; listed in api-types because the Discussion explains it.
