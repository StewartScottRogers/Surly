<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-run-out-of-space-on-upload
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Program finding: the out-of-space refusal reads 552 Upload exceeds the size limit and is logged as the size limit, so a log reader cannot tell the fault from --max-filesize; not claimed in prose beyond the reply text.
2. Uploads off is also exit 25 (550): cased to show only the reply separates them.
