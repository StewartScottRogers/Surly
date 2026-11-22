<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: smtp-log-in-with-cram-md5-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Self-review against REVIEW.md. The raw case uses a TcpClient to send AUTH PLAIN/LOGIN, since Curl.Api will not send a mechanism the server does not offer.
2. Same missing citations as the console twin (RFC 2195, RFC 4954).
3. Bounded wait before reading the server's record (CI race sweep).
