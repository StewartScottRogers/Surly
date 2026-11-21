<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-move-a-mail-client-to-imap-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.
## Findings
1. Carries the console claims; seed_mail, list_mailboxes (message_count, unseen_count) and get_events login_judged by scheme; each curl-mcp call is its own session, so DELE commits at the end of the call.
