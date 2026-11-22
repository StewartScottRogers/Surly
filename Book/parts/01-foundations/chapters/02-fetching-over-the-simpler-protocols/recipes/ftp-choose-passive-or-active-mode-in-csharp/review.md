<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-choose-passive-or-active-mode-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Carries the console claims the library can show: the four modes by recorded exchange, the server log, the refused address (CurlException FtpPortFailed) and the refused login (LoginDenied). One CurlClient reuses its control connection, so the program makes a client per fetch to get an exchange each.
2. LogLevel is in Surl.Output (using Surl.Output;), not Surl.Api.
3. Bounded wait before reading the server's record (CI race sweep).
