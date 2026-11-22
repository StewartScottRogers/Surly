<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-download-in-parallel-segments-in-csharp
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, dotnet 11.0.100: 4 of 4 cases pass on windows.
## Findings
1. Connections without the hold are claimed as 1 to 4; with SurlFault.Delay 1 s, exactly 4.
2. The HEAD request doubles as the warm-up and is not counted (only exchanges that carried a Range).
3. Bounded wait before reading the server's record (CI race sweep).
