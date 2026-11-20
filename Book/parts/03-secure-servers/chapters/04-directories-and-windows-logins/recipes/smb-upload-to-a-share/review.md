<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smb-upload-to-a-share

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 3 of 3 cases pass on windows.

## Findings

1. Upload, replace, HTTP read-back, refusal (exit 9) and size limit (exit 25) are cases; two listeners on one directory because two surl cannot share one.
