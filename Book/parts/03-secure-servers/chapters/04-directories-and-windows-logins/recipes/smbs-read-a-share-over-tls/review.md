<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smbs-read-a-share-over-tls

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 7 of 7 cases pass on windows.

## Findings

1. The plain smb to smbs port case (exit 56) is marked windows only; Linux and macOS not checked here. The TLS wording after curl: (60) is the library's, so only the code is matched.
