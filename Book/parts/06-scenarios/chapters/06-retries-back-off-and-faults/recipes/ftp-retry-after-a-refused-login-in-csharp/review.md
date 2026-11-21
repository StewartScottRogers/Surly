<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-retry-after-a-refused-login-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 14 of 14 cases pass on windows, one claim per case (a label argument prints one line).
## Findings
1. Curl.Api has no retry option; the program catches LoginDenied and retries with a new CurlClient.
2. Found: the server checks the password before refuse-login, so a wrong password never spends the rule (IsRefusedByFault false, Remaining unchanged).
3. A clear password refused for want of AllowPlaintextAuthentication raises no LoginJudged event and no '530 Login incorrect' line.
