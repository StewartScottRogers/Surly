<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-give-up-within-a-time-budget-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 9 of 9 cases pass on windows, one claim per case (a label argument prints one line).
## Findings
1. Untimed warm-up transfer before add_fault; the curl tool call is timed by the agent.
2. transfer has no retry; the agent keeps its own budget with timeoutSeconds 1 per call.
