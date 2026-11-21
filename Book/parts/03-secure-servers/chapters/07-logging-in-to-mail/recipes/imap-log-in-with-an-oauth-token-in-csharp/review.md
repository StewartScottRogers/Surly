<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: imap-log-in-with-an-oauth-token-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 13 cases pass on windows.
## Findings
1. Bibliography has no entry for OAUTHBEARER (RFC 7628) or XOAUTH2; rfc6750 is cited for bearer tokens. Suggest the lead adds rfc7628.
2. CurlClient connection reuse hides a wrong token on a second request; one client per attempt.
4. Curl.Api with Network.UseSsl (STARTTLS) on imap://: the token login was refused (67) where curl --ssl-reqd succeeds; the C# twin makes no claim about it.
