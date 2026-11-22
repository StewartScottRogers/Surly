<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-enforce-a-mailbox-quota-from-an-agent
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 11 of 11, with surl-mcp 1.0.0, curl-mcp cases pass on windows.
## Findings
1. seed_mail into a full mailbox is a tool error 'Mail for ann@example.com was refused: QuotaExceeded.'
2. The console's start-of-log quota line has no MCP equivalent (surl-mcp has no log); not claimed.
