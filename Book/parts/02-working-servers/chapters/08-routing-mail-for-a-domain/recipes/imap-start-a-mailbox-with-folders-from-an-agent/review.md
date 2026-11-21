<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-start-a-mailbox-with-folders-from-an-agent
Verdict: accept with minor findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 9 of 9 cases pass on windows.
## Findings
1. reset_server keeps start folders; shown with seed_mail then reset.
2. reset_server puts back a start folder a client deleted (Drafts), not only empties mailboxes; claimed.
