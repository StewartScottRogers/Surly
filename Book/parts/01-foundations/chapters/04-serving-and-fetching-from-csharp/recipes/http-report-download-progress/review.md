<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-report-download-progress
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Distinct from http-report-progress-of-a-large-upload: download totals, a live per-quarter line, and the 404 that never shows a percentage.
2. Byte counts at 25, 50 and 75 percent vary by run and are matched with a pattern.
