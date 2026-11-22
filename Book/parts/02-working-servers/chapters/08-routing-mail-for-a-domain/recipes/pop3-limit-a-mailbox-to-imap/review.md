<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-limit-a-mailbox-to-imap
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 8 of 8 cases pass on windows.
## Findings
1. Distinct from pop3-move-a-mail-client-to-imap (smtp,imap limit as one step of a move) and imap-keep-a-mailbox-off-smtp: this one shows the POP3 refusal is the same for a limited mailbox, a missing one and a wrong password, client side and in the log.
2. The log is read through wait-for-log.sh (bounded 10 s); the fallback case uses `||`, which cmd.exe and sh both read.
