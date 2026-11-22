<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-resume-an-interrupted-download-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 10 of 10 cases pass on windows.
## Findings
1. REST via PreQuote plus IgnoreContentLength (no resume option in Curl.Api); the REST is read from the recorded exchange. The drop, retry and FtpCouldntRetrFile cases stay in ftp-resume-a-dropped-download-in-csharp.
2. Bounded wait before reading the server's record (CI race sweep).
