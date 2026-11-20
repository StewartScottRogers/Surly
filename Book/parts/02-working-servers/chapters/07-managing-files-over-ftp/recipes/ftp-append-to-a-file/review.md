<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-append-to-a-file
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 11 cases pass on windows.
## Findings
1. curl --append sends APPE, exit 0; the size limit counts the whole file after the append (12 bytes sent to an 11-byte file under --max-filesize 15 is exit 70), which the claim states.
2. Console cases read the result from site/ with expect files, so no script is needed.
