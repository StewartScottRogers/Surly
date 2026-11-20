<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-resume-an-interrupted-download-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077): 6 of 6 cases pass on windows.
## Findings
1. No program finding. surl honours If-Range against its Last-Modified date: a different date gets 200 and the whole file, a matching one 206. HEAD answers Last-Modified.
2. Cases share one working folder, so each that needs the partial file restores it with its own fixtures.
