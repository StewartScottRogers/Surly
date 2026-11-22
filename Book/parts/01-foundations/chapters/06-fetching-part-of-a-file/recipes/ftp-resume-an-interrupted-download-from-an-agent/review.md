<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-resume-an-interrupted-download-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. REST read from get_exchanges and from curl -v; the image is text so it travels through files as a string.
2. Bounded wait before reading the server's record (CI race sweep).
