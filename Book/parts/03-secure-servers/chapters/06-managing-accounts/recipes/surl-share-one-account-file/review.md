<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-share-one-account-file

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 13 of 13 cases pass on windows.

## Findings

1. One surl, four TLS listeners, one --user-file; refusals differ by protocol (401/22, 67, 67, CONNACK 4/8). 2. Not cased: that the file is read once at start, and the Linux and macOS permission warning (cited).
