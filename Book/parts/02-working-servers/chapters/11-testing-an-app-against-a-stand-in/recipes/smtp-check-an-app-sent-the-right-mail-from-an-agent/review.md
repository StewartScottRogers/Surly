<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-check-an-app-sent-the-right-mail-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 7 of 7, with dotnet 11, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core cases pass on windows.
## Findings
1. wait_for_event store_changed with name = owner, then read_message; the never-delivered wait answers timed_out after timeout_ms 2000 (not a tool error).
