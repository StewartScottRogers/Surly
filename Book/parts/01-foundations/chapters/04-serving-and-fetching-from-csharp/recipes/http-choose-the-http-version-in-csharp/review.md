<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-choose-the-http-version-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Response Version for an HTTP/1.0 request is 1.1 (surl answers 1.1 with Connection: close); the recipe reads the server's recorded request line to show what was asked.
2. Program finding: `CurlResponse.Info.HttpVersion` is 0 when read after SendAsync (Info is not final until the transfer ends); the recipe uses `Version`.
3. CurlClientOptions.Http as a client-wide default is cited from the Curl.Api docs, not a case.
