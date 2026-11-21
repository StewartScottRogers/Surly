<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-upload-several-files-at-once-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 6 of 6 cases pass on windows.
## Findings
1. Concurrency is shown by the server's exchange count (more than one when sent together, one when awaited in turn); the count is claimed as 2 or 3, not exactly 3, to stay safe on a slow runner.
