<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-download-every-file-in-a-folder
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. The names come from -l; folders are told from files by the long listing's leading d, since -l alone cannot. Short sh script fixture, needs a POSIX shell; names with spaces are not handled (stated).
