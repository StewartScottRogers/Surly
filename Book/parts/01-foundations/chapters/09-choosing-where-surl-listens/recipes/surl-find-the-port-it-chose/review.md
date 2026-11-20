<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-find-the-port-it-chose
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 3 of 3 cases pass on windows.
## Findings
1. No servers: block: the solution is a POSIX sh script (find-port.sh, a fixture, included in the Solution) that starts its own surl on port 0, so verify runs `sh find-port.sh`; on Windows sh comes from Git. No PowerShell twin; a reader on Windows needs Git Bash or WSL.
2. Found while probing: `surl -s` suppresses the Listening on line (case silent-has-no-line); the script bounds its wait (50 tries) and exits 1.
3. Dropped a claim that two port-0 listen URLs each get their own line in order: the script reads only the first, so no case showed it. The C# and agent twins carry it (a port each).
4. The bind failure uses 192.0.2.1 (TEST-NET-1); surl's reason after the colon is the platform's, so only the prefix is checked.
