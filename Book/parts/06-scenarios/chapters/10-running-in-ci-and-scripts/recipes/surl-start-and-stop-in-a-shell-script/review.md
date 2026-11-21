<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-start-and-stop-in-a-shell-script
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 6 of 6 windows cases pass (7 cases; surl-exit-code-unix is [linux, macos], left to CI).
## Findings
1. Platform finding: from Git Bash on Windows, kill (TERM) ends the native surl outright after about 3 s and wait gives 143; on Linux/macOS surl's help says SIGTERM exits 0. Split into [windows] 143 and [linux, macos] 0 cases; the Linux/macOS case is unproven until CI runs.
2. kill -INT does nothing to surl on Windows (probed, not claimed); the script uses TERM only.
3. The command runs in the background and the script waits, so a TERM to the script acts at once (case interrupted).
4. Uses POSIX sh scripts shown with include::; the Solution says Git Bash is needed on Windows.
Re-pinned to Surl 5da5b3eaf / Curl c84035f797: Surl.Console is unchanged since 9ad60d80c and the interrupted case passes by hand (15 of 15, 2-3 s); the 60 s timeout came under a loaded full run. interrupt.sh now gives up after 10 s if surl never listens and sends TERM a second after the Listening on line, so it never lands before the command has started.
