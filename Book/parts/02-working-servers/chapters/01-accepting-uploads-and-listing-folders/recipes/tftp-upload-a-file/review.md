<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-upload-a-file
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 12 of 12 cases pass on windows.
## Findings
1. Truth: TFTP error 2 is read from `curl -v` (`TFTP error: Access violation`) and exit 69; the packet itself is shown by the C# and agent twins.
2. Program finding: curl cannot upload an empty file over TFTP here (exit 26, local file not opened), so no empty-upload claim is made.
3. Fixed: `ready: none` for the UDP listeners; a server without `--directory` keeps its files in memory, since two surls cannot share one directory.
