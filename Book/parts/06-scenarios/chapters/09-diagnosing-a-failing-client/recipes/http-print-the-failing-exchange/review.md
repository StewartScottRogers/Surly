<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-print-the-failing-exchange
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 5 of 5 cases pass on windows.
## Findings
1. Exchange found as Exchanges[count before], one CurlClient per test; Chunks give crossing order (Received/Sent alone would print both requests before both responses).
2. The in-memory store's 404 log line names a made-up path (C:\surl\Report.csv on Windows); matched loosely.
Re-pinned to Surl a423807a5d / Curl 0680b97577: surl's 401 and 404 now carry a one-line text body (BL-1444); the program now prints body lines too, and the solution's pattern shows the 404's Content-Type, Content-Length 14 and body.
