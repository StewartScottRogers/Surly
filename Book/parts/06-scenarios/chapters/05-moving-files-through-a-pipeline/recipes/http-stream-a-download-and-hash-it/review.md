<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-stream-a-download-and-hash-it

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 2 of 2 cases pass.

## Findings

1. Dropped case prints only the exception type: the CurlException code was RecvError on Windows (connection reset) and may differ by platform.
2. Drop fault is added after the warm-up: an accept-stage rule takes no path and applies to every new connection.
