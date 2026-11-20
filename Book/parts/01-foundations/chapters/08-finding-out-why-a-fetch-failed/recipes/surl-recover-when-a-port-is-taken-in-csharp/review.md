<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-recover-when-a-port-is-taken-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. The holder is a plain TcpListener; surl's bind on the same 127.0.0.1 port fails with 45 Address already in use on Windows; Linux and macOS to be confirmed by CI.
2. SurlServerStartException.Message for a bad port: '(3) URL rejected: Port number was not a decimal number between 0 and 65535', ExitCode MalformedUrl.
