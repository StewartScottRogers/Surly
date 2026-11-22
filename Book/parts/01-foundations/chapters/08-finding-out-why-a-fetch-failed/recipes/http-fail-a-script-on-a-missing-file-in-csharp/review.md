<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fail-a-script-on-a-missing-file-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. DownloadFailedException, its message and exit 22 are the program's choices, stated so.
2. Re-pinned to Surl a423807a5d / Curl 0680b97577: without the check the saved file now holds surl's 14-byte 404 body (BL-1444).
