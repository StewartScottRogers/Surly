<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: pop3-move-a-mail-client-to-imap-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 12 of 12 cases pass on windows.
## Findings
1. Carries the console claims; mail seeded with Mail.DeliverAsync, a fresh CurlClient per request (a kept IMAP connection misses later mail, and POP3 DELE commits on dispose), counts from SurlMailboxInfo, logins from LoginJudgedEvent.Scheme. new HttpMethod("UIDL") on the POP3 root works.
