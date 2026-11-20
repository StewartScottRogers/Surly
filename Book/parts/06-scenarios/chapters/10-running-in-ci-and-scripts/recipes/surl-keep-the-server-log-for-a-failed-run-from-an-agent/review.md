<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-keep-the-server-log-for-a-failed-run-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. surl-mcp has no log file: start_server refuses --log-file/--trace (case no-log-file). The agent keeps get_events whole and get_exchanges with received chunks reduced to a count.
2. Program finding: the refusal text names SurlServerOptions.Log, a .NET property, to an MCP caller.
3. Certificate from setup: openssl, passed as --cert/--key inside SURL_MCP_ROOT; curl tool uses --cacert.
