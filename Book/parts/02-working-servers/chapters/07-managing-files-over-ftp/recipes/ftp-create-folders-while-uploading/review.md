<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-create-folders-while-uploading
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 10 cases pass on windows.
## Findings
1. --ftp-create-dirs makes only the folders that answer 550 on CWD; with --allow-uploads off MKD is 550 Not permitted and curl still exits 9.
2. Listing needs --list-directories on the server; the verbose claims use 2>&1 and a regex over the whole trace.
