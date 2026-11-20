<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-move-a-client-to-https-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 9 of 9 cases pass on windows.

## Findings

1. In process there is no readiness probe, so `Exchanges` by `Scheme` is the record directly.
