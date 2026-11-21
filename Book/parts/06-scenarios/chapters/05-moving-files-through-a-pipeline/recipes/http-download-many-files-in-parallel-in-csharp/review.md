<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-many-files-in-parallel-in-csharp

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 5 of 5 cases pass.

## Findings

1. No-cap case passes on a range (1 to 49 whole, at least one 503); counts vary per run.
2. MaxConnectionsPerHost 8 still churned connections (22 exchanges for 50 requests, Network MaxConnects 8 made no difference) but never got a 503 in four runs.
