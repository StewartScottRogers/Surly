<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-retry-until-the-server-is-up
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 4 of 4 cases pass on windows.
## Findings
1. The race is made certain by start-late.sh (curl first, surl 2 s later); it needs a POSIX shell.
2. The number of retries depends on surl's start time, so it is matched as 1 to 5.
3. Program finding (not claimed): a script that kills a surl it backgrounded a moment earlier can leave that surl running on Windows (msys kill before the process is up). The script only kills after curl has finished, by which time surl is up; the cases that need no server run curl alone.
