<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fetch-a-file-with-an-awkward-name-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 7 of 7 cases pass on windows.
## Findings
1. No program finding. Curl.Api throws CurlException UrlMalformat (3) for a raw space and sends a raw # as a dropped fragment, as curl does.
2. The program prints paths, not names: console output on Windows is not UTF-8, so a printed é would not match on every platform.
3. A raw é in a URL string is not shown: it was 404 on Windows and may differ on Linux and macOS.
