<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-keep-the-server-log-for-a-failed-run-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. surl-mcp has no log file: start_server refuses --log-file/--trace (case no-log-file). The agent keeps get_events whole and get_exchanges with received chunks reduced to a count.
2. get_exchanges still carries raw received bytes by design (Surl ADR-0182 decision 4), so the agent's filtering is still needed although surl's own log and trace now withhold passwords; the Discussion says which is which.
3. Certificate from setup: openssl, passed as --cert/--key inside SURL_MCP_ROOT; curl tool uses --cacert.
4. Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: start_server's refusal of --log-file is now in surl-mcp's own terms (no .NET property named); the case and Discussion quote it.
5. Bounded wait before reading the server's record (CI race sweep).
