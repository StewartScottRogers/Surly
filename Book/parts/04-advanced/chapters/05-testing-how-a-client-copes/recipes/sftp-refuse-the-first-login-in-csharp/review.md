<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-refuse-the-first-login-in-csharp
Verdict: accept with minor findings (self-review)

Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100: 4 of 4 cases pass on windows.

## Findings

1. A planned case (`CurlSshOptions.AuthenticationTypes = SshAuthenticationTypes.Password` makes one attempt a connection, so `Times = 1` suffices) was dropped: Curl.Api still sends keyboard-interactive after the password is refused, unlike upstream libcurl. Reported as a Curl finding.
2. Adds what the console twin cannot show: `LoginJudgedEvent.IsRefusedByFault`, and that a wrong password does not spend the rule (`Remaining` stays 2).
3. `--retry-all-errors` has no Curl.Api counterpart; the program retries `LoginDenied` in its own loop.
