<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-skip-unchanged-files-with-etags

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 7 of 7 cases pass.

## Findings

1. Nearest honest recipe: the plan asked for ETags; the pinned surl sends Last-Modified and no ETag and answers If-Modified-Since/If-None-Match with 200 (BL-1446..1449 in progress). Retitled 'Skipping Unchanged Files by Date'; key kept; the plan row's title needs the lead's change.
2. Cases hold whether surl later answers 304 itself or not: curl -z reports 304 and keeps the local file either way. No case claims the absence of conditional support.
3. -R after a skipped fetch moves the local date back to the server's older date (shown by the If-Modified-Since case).
