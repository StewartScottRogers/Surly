<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-log-in-with-cram-md5-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The raw case goes through the curl tool's telnet:// support; get_exchanges/get_events show the server's side.
2. Bounded wait before reading the server's record (CI race sweep).
