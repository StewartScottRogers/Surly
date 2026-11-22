<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: tftp-choose-a-block-size-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. transfer has no TFTP options, so the block size is chosen with the curl tool; the transfer-tool case says so.
2. files takes text only; a binary file goes in files_base64, as the Discussion says.
3. Not run on Linux or macOS.
4. Bounded wait before reading the server's record (CI race sweep).
