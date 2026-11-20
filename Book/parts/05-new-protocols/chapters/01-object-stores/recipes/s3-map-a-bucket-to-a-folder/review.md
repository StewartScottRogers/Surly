<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: s3-map-a-bucket-to-a-folder

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 9 of 9 cases pass on windows.

## Findings

1. Decision: `--bucket pics=/gallery` takes a content path with a leading slash; `pics=gallery` is exit 2 `is not a content path` (probed, not claimed). In Git Bash on Windows the slash is rewritten to a disk path unless `MSYS_NO_PATHCONV=1` is set; the Discussion says so (a cited platform tip, not a case).
2. Left out: mapping onto a path inside a `--share` (`/team/photos`), and an in-memory root, where the folder is made empty (aihelp content, not probed).
3. The `missing-folder` case uses its own folder `spare` because the main server holds `data`'s lock.
