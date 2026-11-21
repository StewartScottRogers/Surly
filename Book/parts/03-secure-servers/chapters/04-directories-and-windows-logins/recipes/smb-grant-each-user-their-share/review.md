<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smb-grant-each-user-their-share

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 14 of 14 cases pass on windows.

## Findings

1. --share and --share-access give per-account rights: r reads, rw also writes (still needs --allow-uploads); an unnamed account sees the share as missing (78), not denied; a share no grant names stays open and takes uploads.
