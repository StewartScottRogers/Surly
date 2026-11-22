<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-compare-a-good-and-a-bad-request-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 6 of 6 cases pass on windows.
## Findings
1. Curl.Api sends a header name with a trailing space as given (`X-Api-Key : k123`) and `RemovedHeaders = ["Host"]` drops Host; both are 400 with the log reasons the console twin shows.
2. Curl.Api sends no User-Agent, so 3 shared lines here against 4 on the console.
