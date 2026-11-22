<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-handle-a-full-disk-during-upload-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 8 of 8 cases pass on windows.
## Findings
1. Mid-upload refusal exitCode 70 or 55 by the same race; the case matches either.
Re-pinned to Surl a423807a5d / Curl 0680b97577: the 404 read now has the body 404 Not Found (BL-1444), the program prints it; the partway upload is always exitCode 70 (Curl BL-5663).
