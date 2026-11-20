<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-rename-and-delete-a-file
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 13 cases pass on windows.
## Findings
1. curl -Q without a prefix runs before the transfer and with a dash after it; the cases show it with a listing, which needs --list-directories.
2. Rename onto a taken name replaces it with no refusal; a missing source is 550 and exit 21; RNTO alone is 503.
