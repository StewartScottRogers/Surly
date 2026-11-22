<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-a-test-server-in-ci-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. StopAsync returns Ok after a passing and a failing run; the non-Ok branch has no case (no way found to make StopAsync fail) and is described as what the code does.
2. A TcpListener holds the port for the BindFailed case, as in surl-recover-when-a-port-is-taken-in-csharp.
3. Bounded wait before reading the server's record (CI race sweep).
