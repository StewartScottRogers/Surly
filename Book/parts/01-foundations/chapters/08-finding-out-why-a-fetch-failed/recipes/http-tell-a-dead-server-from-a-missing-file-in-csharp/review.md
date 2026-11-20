<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-a-dead-server-from-a-missing-file-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 5 of 5 cases pass on windows.

## Findings

1. CurlException.Code prints as CouldntConnect (7) and CouldntResolveHost (6); a 404 never throws.
2. Exit 7 is TCP, the same on every platform.
