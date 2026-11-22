<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-check-an-app-uploaded-the-right-file-in-csharp
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port (Curl commit c84035f797), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core: 7 cases pass on windows.
## Findings
1. `Files.ReadAsync` reads the store after the app's PUT has returned 226, so no event wait is needed for it; the HTTP view of the same file is its own case.
2. A missing file is `SurlNotFoundException`; uploads off is `RemoteAccessDenied` 9 because `CreateMissingDirectories` asks for MKD first.
