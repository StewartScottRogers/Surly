<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-choose-passive-or-active-mode-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Modes are chosen with the curl tool and -P / --disable-epsv / --disable-eprt; the transfer tool has no FTP options. The server's log of the data connection is not reachable through surl-mcp (get_events has only exchange start and end), so that console claim is not carried; the commands in get_exchanges and curl -v carry the mode instead.
2. Bounded wait before reading the server's record (CI race sweep).
