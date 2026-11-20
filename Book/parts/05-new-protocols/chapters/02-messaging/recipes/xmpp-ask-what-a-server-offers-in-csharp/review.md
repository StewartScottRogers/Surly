<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: xmpp-ask-what-a-server-offers-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. Program uses a raw `TcpClient` for the items answer on a server with `AllowPlaintextAuthentication`; the reader's quirk is that it reads until the server pauses 500 ms, said in the Discussion.
2. Curl.Api maps stanza errors to `CurlException` code 8 `WeirdServerReply` and SASL failures to code 67 `LoginDenied`, with the console messages. `?disco;items` through Curl.Api is info again.
3. TLS: Curl.Api trusts server.pem through `CurlTlsPeerOptions.CertificateAuthorityFile`; passes the same as the console on Windows; Linux and macOS come from CI.
