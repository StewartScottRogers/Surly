<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-handle-a-full-disk-during-upload
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. The out-of-space fault answers 552 at STOR before any data; a mid-upload full disk is shown with --max-filesize (partial deleted).
2. Program finding: the log line for the out-of-space refusal is the size-limit one ("The upload grew past the limit of 100000 bytes; the partial upload was deleted") and names no fault rule; cased as it is.
3. Past --max-filesize curl exits 70 or 55 by a race (data connection reset vs 552); the case checks only that the upload failed.
4. Over HTTP the same rule answers PUT 413, not 507.
