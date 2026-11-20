<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-rename-and-delete-a-file-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 7 cases pass on windows.
## Findings
1. Quote and PostQuote are -Q and -Q with a leading dash; every failed quoted command is CurlException QuoteError 21.
2. The replies 350 and 250 are Headers entries keyed by the reply line.
