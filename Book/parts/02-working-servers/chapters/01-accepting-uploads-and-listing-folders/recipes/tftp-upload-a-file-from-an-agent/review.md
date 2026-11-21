<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-upload-a-file-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 12 of 12 cases pass on windows.
## Findings
1. Truth: the error code 2 is read from `get_exchanges` (base64 datagrams); a missing store path is a `read_file` tool error, printed as nothing stored.
2. Reader: the local files to send are written by the program into the folder both hosts are confined to.
