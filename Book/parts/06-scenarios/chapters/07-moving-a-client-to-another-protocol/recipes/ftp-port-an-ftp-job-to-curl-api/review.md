<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-port-an-ftp-job-to-curl-api
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 of 11 cases pass on windows.
## Findings
1. The 'other way' is FtpWebRequest (obsolete; SYSLIB0014 suppressed around it). Both jobs run against one Surl.Api stand-in with AllowPlaintextAuthentication; recorded exchanges show 4 connections (PASV, NLST) for FtpWebRequest against 1 (EPSV, LIST) for one CurlClient.
2. The FtpWebRequest connection count (one per request) is .NET's managed behaviour; checked on Windows only, CI will show Linux and macOS.
