<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-check-unread-counts-from-an-agent
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 9 of 9 cases pass on windows.
## Findings
1. seed_mail then wait_for_event on `ann@example.com/INBOX/;UID=3` before counting, per the batch note on asynchronous delivery.
2. The curl tool's fetch writes message1.eml into the folder (-o); harmless, and keeps the body out of the JSON.
