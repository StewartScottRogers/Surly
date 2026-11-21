<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-handle-a-full-disk-during-upload-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.
## Findings
1. Mid-upload refusal is RemoteDiskFull or SendError by the same race; the case matches either.
Re-pinned to Surl a423807a5d / Curl 0680b97577: the 404 read now has a 14-byte body (BL-1444); the partway upload always throws RemoteDiskFull (Curl BL-5663); the log line names the rule, so 'naming no rule' was dropped.
