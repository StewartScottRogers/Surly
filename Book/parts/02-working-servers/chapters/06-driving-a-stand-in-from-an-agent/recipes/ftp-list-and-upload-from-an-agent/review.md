<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-list-and-upload-from-an-agent
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 cases pass on windows.
## Findings
1. curl-mcp curl tool handles -l and -T; a refused listing, upload or login is a tool error with the curl exit code (19, 25, 67).
2. The long listing is checked by type letter and name only, since size and date vary.
