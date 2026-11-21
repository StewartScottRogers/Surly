<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-give-up-within-a-time-budget
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 7 of 7 cases pass on windows.
## Findings
1. Retry counts under a budget are claimed as a range (1 to 3) and whole-command times as ranges from a sh script (date +%s), since the first transfer of a curl process carries about 4 s of set-up.
2. Curl port's --retry-max-time (TransferRetrier): checked after each failed try; a decided retry still runs, so the loop overruns by at most one wait and one try. Stated as such.
3. Two rules apply to one try: delay at accept and error at command (different stages).
