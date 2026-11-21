<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-log-in-to-mail-with-a-token
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 11 cases pass on windows.
## Findings
1. Bibliography has no entry for OAUTHBEARER (RFC 7628) or XOAUTH2; rfc6750 is cited for bearer tokens. Suggest the lead adds rfc7628.
2. Program finding: with --auth oauthbearer,xoauth2 alone, IMAP LOGIN and POP3 USER/PASS over TLS still accept a mailbox password (curl falls back to LOGIN); the scenario closes it with protocols=http, case why-protocols-http records the behaviour.
3. Curl.Api IMAP failure message after a refused LOGIN carries a stray control character (Access denied. then 0x02); the C# cases print the code only.
