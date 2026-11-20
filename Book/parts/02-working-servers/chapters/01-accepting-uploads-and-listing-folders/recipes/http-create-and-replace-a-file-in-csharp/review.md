<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-create-and-replace-a-file-in-csharp
Verdict: accept with findings
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 3 of 3 cases pass on windows.
## Findings
1. Files.ReadAsync throws SurlNotFoundException (not null) for a missing file and for a folder; the program reports both as 'no file'.
2. SendAsync throws for none of 201, 204, 405, 409: the test reads StatusCode.
