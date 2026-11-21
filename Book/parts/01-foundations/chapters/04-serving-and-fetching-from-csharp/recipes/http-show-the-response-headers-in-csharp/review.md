<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-show-the-response-headers-in-csharp
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. Date and Last-Modified values are matched as patterns; header order is claimed and checked exactly.
Re-pinned to Surl a423807a5d / Curl 0680b97577: 200 adds ETag before Last-Modified (Surl BL-1449); 404 now has Content-Type text/plain; charset=utf-8, Content-Length 14 and the body 404 Not Found (BL-1444).
