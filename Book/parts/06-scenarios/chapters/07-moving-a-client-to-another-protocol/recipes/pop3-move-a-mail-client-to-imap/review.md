<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-move-a-mail-client-to-imap
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.
## Findings
1. One surl, SMTP listed first so readiness spends no POP3 session; the seed waits over IMAP (FETCH 2 FLAGS sets no flag), so the Maildrop-opened count is exactly the cases' four. Cutover closes POP3 with ;protocols=smtp,imap.
2. IMAP SEARCH answers sequence numbers, so after a POP3 DELE the survivor is 1; the delete case uses STATUS instead.
