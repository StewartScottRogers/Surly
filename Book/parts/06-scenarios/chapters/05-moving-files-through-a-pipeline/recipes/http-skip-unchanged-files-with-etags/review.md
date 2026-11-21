<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-skip-unchanged-files-with-etags

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit a423807a5d), Curl port (Curl commit 0680b97577), local builds, 2026-11-21, windows: 10 of 10 cases pass.

## Findings

1. Re-pinned to Surl a423807a5d / Curl 0680b97577: surl now sends a strong ETag and answers If-None-Match with 304 (BL-1449), so the recipe does its planned ETag task with --etag-compare/--etag-save; title restored to 'Skipping Unchanged Files with ETags' (and the plan row). The restored-with-older-date file is now fetched; a -z case keeps the contrast.
2. Without -f, a 404 now writes surl's short body (`404 Not Found`, BL-1444) over the local copy, not an empty file; --etag-save leaves the tag file as it was on a 404.
3. The tag file's exact value depends on the file's mtime in the runner's time zone, so the case matches its shape only.
