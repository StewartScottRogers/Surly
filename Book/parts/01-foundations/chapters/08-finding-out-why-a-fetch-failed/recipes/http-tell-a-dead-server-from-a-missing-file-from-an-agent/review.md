<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-a-dead-server-from-a-missing-file-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 4 of 4 cases pass on windows.

## Findings

1. After stop_server, transfer has statusCode null and failure kind CurlExit (tool error); the curl tool gives exitCode 7 and -w output 000 7.
2. Exit 7 is TCP, the same on every platform.
