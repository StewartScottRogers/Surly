<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-report-progress-of-a-large-upload

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 2 of 2 cases pass.

## Findings

1. Uses a locked IProgress implementation instead of Progress<T>, whose posting could reorder or delay reports; the reason is from Curl.Api's documentation of CurlRequest.Progress.
2. Refused case: 405 before the body; reports stay at Connecting, 0 of 0.
