<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-check-an-app-cleaned-up-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 9 cases pass on windows.
## Findings
1. `list_files` needs no `--list-directories`; curl -l without it is exit 19 `RETR response: 550`.
2. With uploads off both of the app's uploads are refused, so curl prints two 550 lines.
