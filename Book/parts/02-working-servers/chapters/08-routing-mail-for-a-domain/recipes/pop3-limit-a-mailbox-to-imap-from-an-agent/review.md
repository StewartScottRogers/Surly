<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-limit-a-mailbox-to-imap-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 7 of 7, with dotnet 11, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core cases pass on windows.
## Findings
1. get_events login_judged also carries is_refused_by_fault (false here); not claimed.
