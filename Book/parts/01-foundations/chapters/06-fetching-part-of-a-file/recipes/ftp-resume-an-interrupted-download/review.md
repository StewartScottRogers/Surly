<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-resume-an-interrupted-download
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.
## Findings
1. The plan's 'cut off' download is the basic task (a partial file made in setup, curl -C -); a download cut mid-file by the server is not possible (Surl BL-1518), and ftp-resume-a-dropped-download already covers drop, retry and exit 36. This recipe adds REST in -v, no-partial, without -C, an explicit offset and the trusted-prefix edge.
