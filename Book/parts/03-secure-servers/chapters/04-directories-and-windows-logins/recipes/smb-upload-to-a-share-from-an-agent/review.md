<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smb-upload-to-a-share-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 3 of 3 cases pass on windows.

## Findings

1. Upload over SMB is done by curl-mcp's curl tool with -T; each mode has its own directory because cases share a case root.
