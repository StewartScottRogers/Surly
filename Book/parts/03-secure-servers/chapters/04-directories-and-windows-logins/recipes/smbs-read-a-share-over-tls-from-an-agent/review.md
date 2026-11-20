<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smbs-read-a-share-over-tls-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 cases pass on windows.

## Findings

1. Covers the same claims through start_server (tool error with exit_code 58) and the curl tool.
