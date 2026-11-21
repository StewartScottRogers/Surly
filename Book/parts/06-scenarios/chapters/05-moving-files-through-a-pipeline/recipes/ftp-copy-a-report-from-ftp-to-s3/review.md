<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-copy-a-report-from-ftp-to-s3
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 8 of 8 cases pass on windows.
## Findings
1. verify reads files: expectations as text (CR LF folded), so byte identity with Windows line ends is shown with cmp cases, not files: content.
2. Without --fail-early curl uploads a stale local file after a failed download and exits 0; cased as the recipe's failure path.
