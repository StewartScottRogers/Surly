<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-retry-until-the-server-is-up-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. The port is agreed in advance with a TcpListener probe on port 0, released before either side uses it.
2. The program retries only CurlCode.CouldntConnect (a when filter).
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: under load the first attempt got 200, because the first Curl.Api request's cold start (4 s even on an idle machine) outlasted the server's two-second head start; not a Curl regression. The server's two seconds now count from the first refused attempt.
