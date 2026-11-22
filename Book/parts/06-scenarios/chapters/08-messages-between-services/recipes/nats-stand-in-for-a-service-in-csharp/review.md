<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: nats-stand-in-for-a-service-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. The stand-in is a TcpClient NATS session in the program; the app is a Curl.Api POST to ?request. Server record read from Exchanges[].Received, polled for at most 5 s.
2. A warm-up publish comes before the 3-second Timeout case.
