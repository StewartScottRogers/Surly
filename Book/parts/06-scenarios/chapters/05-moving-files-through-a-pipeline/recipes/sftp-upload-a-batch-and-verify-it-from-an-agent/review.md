<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-upload-a-batch-and-verify-it-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), OpenSSH 10.5p1 (Git for Windows), dotnet, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows.
## Findings
1. Program finding: curl-mcp's `curl` tool refuses a glob in `-T` (`failure.kind` PathRefused: names files with glob characters whose expanded paths cannot be checked), so the agent uploads with one call holding a `-T` pair for each file; case `glob-refused`.
2. Truth: the comparison is checked twice, by curl's listing and by surl-mcp's `list_files` byte counts.
3. Re-pinned to Surl a423807a5d / Curl 0680b97577: curl-mcp now accepts -T globs, checking each expansion: the glob-refused case became a glob upload case plus a PathRefused case for an expansion outside CURL_MCP_ROOT.
