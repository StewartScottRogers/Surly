<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-keep-a-mailbox-off-smtp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 8 cases pass on windows.
## Findings
1. `;protocols=gopher` is accepted at start (surl listens and runs) although --aihelp says an unknown scheme exits 2; not recorded as a claim.
2. A refused SMTP login is the same 535 5.7.8 as a wrong password, so the closed protocol does not reveal the account.
