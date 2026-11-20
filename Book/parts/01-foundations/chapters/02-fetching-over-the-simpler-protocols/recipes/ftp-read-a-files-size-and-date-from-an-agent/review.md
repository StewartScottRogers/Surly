<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-read-a-files-size-and-date-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. curl -I and -R both work through the curl tool; the copy's time is read back from disk, exact (2024-01-02T15:04:05Z), because the program sets the served file's time itself.
