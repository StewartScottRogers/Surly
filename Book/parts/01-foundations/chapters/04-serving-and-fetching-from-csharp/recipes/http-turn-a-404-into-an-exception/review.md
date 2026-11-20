<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-turn-a-404-into-an-exception
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 4 of 4 cases pass on windows.
## Findings
1. The helper throws a NotFoundException derived from HttpRequestException (StatusCode NotFound), a plain HttpRequestException for other error statuses (503 via a SurlFault), and leaves CurlException for an unreachable server alone; each is a case.
2. Removed an unclaimed remark that surl sends no body with a 404.
