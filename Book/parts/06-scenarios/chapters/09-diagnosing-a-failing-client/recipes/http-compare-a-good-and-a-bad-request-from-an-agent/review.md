<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-compare-a-good-and-a-bad-request-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 7 of 7 cases pass on windows.
## Findings
1. curl-mcp `transfer` refuses a header name with a trailing space (`InvalidRequest`, `'X-Api-Key ' is not a header name.`), so the bad client goes through the `curl` tool; the refusal is its own case.
2. The console's "why" claim (the log's WhitespaceBeforeColon line) cannot be carried: surl-mcp offers no log. The agent shows the recorded 400 head instead and says so.
