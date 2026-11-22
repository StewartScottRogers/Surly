<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-enforce-a-mailbox-quota
Verdict: accept with minor findings
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797): 11 of 11 cases pass on windows.
## Findings
1. quota= on --mailbox: the SMTP refusal is 452 4.2.2 Mailbox full after the end of DATA (RCPT is 250), so curl exits 8 Weird server reply, not 55. Real curl does the same; worth a sentence in Surl's help.
2. Stored size (Return-Path, Received) counts; claimed as a range (4[5-6]x) since the Received date can change length.
3. One full mailbox refuses the whole message for every recipient; \Deleted still counts, EXPUNGE frees room.
