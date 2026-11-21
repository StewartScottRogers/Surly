<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-upload-and-fetch-over-http-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 cases pass on windows.
## Findings
1. One start_server with ftp and http listen URLs shares one store; get_exchanges with scheme splits the two listeners. The verify run was slow (over 2 minutes for 6 cases on a loaded machine) but passed.
