<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-move-logins-from-basic-to-digest-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 13 of 13 cases pass on windows.
## Findings
1. Carries the console claims; logins read from LoginJudgedEvent (Mechanism, Account), the post-cutover Basic requests from server.Exchanges, since no login is judged for them. HttpAuthenticationMethods.Any is --anyauth and picks Digest.
