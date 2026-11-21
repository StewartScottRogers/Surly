<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smb-grant-each-user-their-share-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 6 of 6 cases pass on windows.

## Findings

1. Each mode has its own folders (cases share a root and a share folder takes a lock); the grants go in start_server arguments.
