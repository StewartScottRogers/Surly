<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-stop-a-server-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, stop-server.cs, starts and stops a server in every scenario and then does what the argument picks; every earlier claim is still a case. Each pattern captures the server's ID and URL once and requires every later line to name the same ones.
2. Fixed: earlier finding 2 (list_servers no longer showing a stopped server could not be a case) is resolved: the restart case's anchored pattern allows exactly one running server, the new one, and the Discussion says so. The solution also now checks exchange_count 0 before any request.
3. Fixed: the restart case now also checks that the new server serves its own file (hello again) and that the program stops it before exiting.
4. Only exit code 0 from stop_server is claimed. No way was found to make a clean stop answer anything else.
5. Bounded wait before reading the server's record (CI race sweep).
