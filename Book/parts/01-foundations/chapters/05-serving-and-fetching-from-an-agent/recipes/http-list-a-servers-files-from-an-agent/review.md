<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-list-a-servers-files-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. list_files on a file path answers "No folder at" the path, the same as a missing folder; not claimed separately.
2. Removed a draft sentence saying list_files leaves no exchange on the server: no case showed it.
