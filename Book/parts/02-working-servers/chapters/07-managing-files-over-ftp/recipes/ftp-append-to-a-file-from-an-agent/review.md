<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-append-to-a-file-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 7 cases pass on windows.
## Findings
1. The line to send is written to the confined folder by the program before curl-mcp reads it with -T.
2. read_file answers text; get_exchanges gives APPE and STOR lines across both exchanges.
