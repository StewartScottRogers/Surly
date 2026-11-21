<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-several-files-at-once
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows.
## Findings
1. Peak overlap is read from surl's --trace-ascii (opened/ended and held/answered lines) in a script that starts its own surl; connections are claimed as 4 or 5 under --parallel-max 4 (4 in every run seen), requests exactly 4.
2. Observation: --parallel alone over 12 files ran 11 at once in every run, not 12; claimed as more than 4.
3. Needs a POSIX shell (Git Bash on Windows); each case takes about 8 s because of the 1 s hold and the 3 s Windows kill.
