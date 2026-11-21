<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-skip-unchanged-files-with-etags-from-an-agent

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit a423807a5d), Curl port (Curl commit 0680b97577), local builds, 2026-11-21, windows: 5 of 5 cases pass.

## Findings

1. Re-pinned to Surl a423807a5d / Curl 0680b97577: the curl tool now runs --etag-compare/--etag-save and surl answers 304 itself (BL-1449); the 1.1 s wait before put_file is gone; title restored to 'Skipping Unchanged Files with ETags' (and the plan row). The dates case became a tags case.
2. Without -f, a 404 now writes surl's short body (`404 Not Found`, BL-1444) over the local copy.
3. No older-date case: put_file cannot set a date.
