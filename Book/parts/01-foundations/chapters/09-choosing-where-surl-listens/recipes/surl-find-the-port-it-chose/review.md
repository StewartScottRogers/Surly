<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-find-the-port-it-chose
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 3 of 3 cases pass on windows.
## Findings
1. No servers: block: the solution is a POSIX sh script (find-port.sh, a fixture, included in the Solution) that starts its own surl on port 0, so verify runs `sh find-port.sh`; on Windows sh comes from Git. No PowerShell twin; a reader on Windows needs Git Bash or WSL.
2. Found while probing: `surl -s` suppresses the Listening on line (case silent-has-no-line); the script bounds its wait (about 5 s by the clock) and exits 1.
3. Dropped a claim that two port-0 listen URLs each get their own line in order: the script reads only the first, so no case showed it. The C# and agent twins carry it (a port each).
4. The bind failure uses 192.0.2.1 (TEST-NET-1); surl's reason after the colon is the platform's, so only the prefix is checked.
5. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: silent-has-no-line timed out because the wait counted 50 tries of grep, kill and sleep, each a process start on Git Bash (12 s idle here, past 30 s under load); the script now waits about 5 s by date +%s. Not a surl change.
