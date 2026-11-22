<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-repeat-a-failure-with-a-fixed-seed-in-csharp
Verdict: accept with fixes
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Surl.Api, Curl.Api, surl-mcp and curl-mcp from the same commits, Windows.
## Findings
6 of 6 cases pass on Windows.
1. Same S3-for-HTTP/FTP substitution as the console twin (finding 1 there).
2. The console's no-seed case is left out: the run's exit code then depends on the drawn seed, and a case needs one exit code.
3. Same seed draws the same UploadId as the console (exchange 1), shown by the exact values.
