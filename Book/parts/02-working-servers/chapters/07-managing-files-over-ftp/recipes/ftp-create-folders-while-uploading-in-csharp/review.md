<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-create-folders-while-uploading-in-csharp
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 5 cases pass on windows.
## Findings
1. CreateMissingDirectories = Create is --ftp-create-dirs (libcurl CURLFTP_CREATE_DIR); None is exit 9, RemoteAccessDenied.
2. Files.ListAsync lists each level without needing ListDirectories.
