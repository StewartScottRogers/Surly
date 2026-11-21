<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-retrieve-and-delete-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 10 of 10 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The task as planned (retrieve, delete, confirm with list_messages) is the solution case; the others show listing, numbers against UIDs, RSET, a missing number and the deletion event.
2. There is no delete-message tool in surl-mcp; deletion is a POP3 DELE through the curl tool, committed when the curl run ends. A deleted message is visible in the store at once and its store_changed event (change deleted) is waited for with a bound.
3. POP3 octet counts equal list_messages sizes (message with CRLF line ends, as seeded).
