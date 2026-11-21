<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-many-files-in-parallel-from-an-agent

Verdict: accept with minor findings (author's draft; self-review against REVIEW.md)

Checked against surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), local builds, 2026-11-20, windows: 5 of 5 cases pass.

## Findings

1. Uses a limit of four (not eight as its twins): with eight, the uncapped case got as few as one 503 per run, too thin a margin.
2. Files are 50,000 characters: with 100,000-character files (5 MB of start_server arguments) the call never returned; reported as a program finding.
