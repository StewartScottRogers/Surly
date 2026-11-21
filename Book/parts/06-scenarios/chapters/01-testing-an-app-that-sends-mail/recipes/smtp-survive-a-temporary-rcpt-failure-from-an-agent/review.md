<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-survive-a-temporary-rcpt-failure-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The fault is added with add_fault after start, so no readiness question arises.
2. The agent does the retry itself (curl tool, -w %{response_code}, retry on 4xx), matching the console script rather than curl's --retry-all-errors.
3. A curl tool call that exits 55 still answers JSON with exitCode; the program reads it rather than IsError.
