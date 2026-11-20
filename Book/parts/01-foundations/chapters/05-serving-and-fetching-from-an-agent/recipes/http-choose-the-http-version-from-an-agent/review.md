<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-choose-the-http-version-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Nearest honest recipe: curl-mcp's transfer has no HTTP version field, so versions are chosen with the curl tool; a case shows transfer speaks HTTP/1.1.
2. HTTP/2 request chunks come back from get_exchanges base64-encoded; the program decodes them.
