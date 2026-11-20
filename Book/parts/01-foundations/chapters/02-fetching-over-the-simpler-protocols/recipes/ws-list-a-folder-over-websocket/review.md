<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ws-list-a-folder-over-websocket
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 8 of 8 cases pass on windows.
## Findings
1. Cases that chain with && or | need a POSIX shell (Git Bash on Windows); the Solution says so.
2. Two surls cannot share a directory, so the listings-off server serves a copy of the fixtures.
3. Program finding, not a defect: a folder, a missing path and a dot file are all a 404 to the upgrade, so a client cannot tell listings off from a wrong path.
