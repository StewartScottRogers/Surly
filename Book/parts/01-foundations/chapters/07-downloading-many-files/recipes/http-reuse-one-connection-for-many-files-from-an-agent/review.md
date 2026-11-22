<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-reuse-one-connection-for-many-files-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0, dotnet 11.0.100: 10 of 10 cases pass on windows.
## Findings
1. Each transfer call is its own connection (three calls, three exchanges); reuse needs one curl tool call.
2. curl tool standardOutput has CRLF line ends on Windows; the program trims.
3. Extended to ten cases, one claim each, carrying every console claim curl-mcp can show (glob, URLs, separate calls, Connection: close, --http1.0, --rate within and past --idle-timeout).
4. Bounded wait before reading the server's record (CI race sweep).
