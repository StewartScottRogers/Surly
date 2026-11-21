<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-see-both-sides-of-a-request-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0, dotnet 11.0.100: 4 of 4 cases pass on windows.
## Findings
1. Finding for Curl: curl-mcp's `-v` lines in standardError end in CR CR LF on Windows (`> PUT /notes.txt HTTP/1.1\r\r\n`); expected one CR LF. The program trims.
2. Finding for Curl: the Digest Authorization header differs between curl.exe (`username="alice",realm=...`, hex cnonce) and curl-mcp's curl tool (`username="alice", realm=...`, base64 cnonce, algorithm last) at the same Curl commit. Both are accepted; not claimed.
