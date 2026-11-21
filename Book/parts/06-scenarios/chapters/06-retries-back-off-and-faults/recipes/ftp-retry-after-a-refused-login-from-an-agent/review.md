<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-retry-after-a-refused-login-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 13 of 13 cases pass on windows, one claim per case (a label argument prints one line).
## Findings
1. add_fault after start_server; curl tool carries --retry-all-errors; transfer takes the account in the URL and needs the agent's own loop.
2. get_events login_judged with is_refused_by_fault tells the rule's refusal from a wrong password's.
