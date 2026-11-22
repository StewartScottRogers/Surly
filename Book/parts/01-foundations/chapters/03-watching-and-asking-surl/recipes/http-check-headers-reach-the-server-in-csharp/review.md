<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-check-headers-reach-the-server-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Curl.Api has no client-wide default headers (CurlClientOptions has none), so 'adds headers on every request' is a helper, Build, that stands in for the code under test.
2. The test joins every exchange's Received bytes and splits at blank lines; safe only because these requests have no body, as the Discussion says.
3. Bounded wait before reading the server's record (CI race sweep).
