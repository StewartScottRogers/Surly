<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-transient-from-permanent-errors-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 11 of 11 cases pass on windows, one claim per case (a label argument prints one line).
## Findings
1. Requests counted from get_exchanges received chunks; fault_applied grouped by rule_id.
