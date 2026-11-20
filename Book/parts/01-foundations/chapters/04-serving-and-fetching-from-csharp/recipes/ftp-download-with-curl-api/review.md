<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-download-with-curl-api
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 7 of 7 cases pass on windows.
## Findings
1. AllowPlaintextAuthentication is a loosening option: the case without it (refused 67) and the solution with it are both present.
2. A request with no Authentication and one with the wrong password both throw LoginDenied, so the exception cannot tell them apart.
