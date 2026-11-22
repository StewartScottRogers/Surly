<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-list-and-upload-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 7 cases pass on windows.
## Findings
1. Curl.Api has no names-only listing (the -l of the console recipe): the GET on a folder URL is the long listing, read here as type letter and name.
2. Listings off is CurlException 19 FtpCouldntRetrFile; uploads off 25 UploadFailed; no anonymous login 67 LoginDenied; missing folder 9 RemoteAccessDenied.
3. Bounded wait before reading the server's record (CI race sweep).
