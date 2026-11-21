<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-answer-try-later-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 11 of 11 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. Same 11 claims as the console recipe; the log claims are shown through get_events and get_exchanges, since the MCP host has no log file to grep.
2. Delivery is waited for with wait_for_event, after_sequence and a ten-second bound; a refused send does not wait.
3. A curl tool call that exits 55 still answers JSON with exitCode; the program reads it rather than IsError.
