<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-choose-how-curl-walks-folders
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 14 of 14 cases pass on windows.
## Findings
1. The three methods are shown by -v on the client and by the server's log read with grep; a missing-CWD claim needed a server that dislikes CWD, made with an error fault scoped by path (no stage key). The chain of three fetches uses a folder each because curl reuses the connection and skips or reshapes CWDs.
2. --fresh-connect is unknown to the Curl port, so the reuse could not be switched off.
3. CI run 44898744862: multicwd-steps matched two -v lines with the blank line Windows puts between them, so it failed on Linux and macOS; it now pipes 2>&1 through grep for the CWD and RETR lines and matches them exactly (now also checks the bare RETR one.txt).
