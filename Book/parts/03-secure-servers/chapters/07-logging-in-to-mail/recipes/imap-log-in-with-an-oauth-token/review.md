<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-log-in-with-an-oauth-token
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 13 cases pass on windows.
## Findings
1. Bibliography has no entry for OAUTHBEARER (RFC 7628) or XOAUTH2; rfc6750 is cited for bearer tokens. Suggest the lead adds rfc7628.
2. A token is an account of its own: the user name the client gives picks the mailbox it reads (bob with ann's token: 78). Stated plainly in the Discussion.
3. curl --next reuses a logged-in IMAP connection, so a wrong token on a second transfer succeeds; cases are separate runs.
