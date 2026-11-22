<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-run-curl-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, run-curl.cs, sends one argument list per scenario and prints exitCode, both streams as JSON spells them, and the failure; every earlier claim is still a case.
2. Fixed: the write case now also checks that hello.txt in the root holds the body, so "-o writes the body to a file" is tested, not inferred from standard output.
3. Fixed: the -S case now checks that the message is in standardError as well as in the failure text; the -f case checks the failure text names HttpReturnedError. The Discussion says both.
4. curl-mcp ends standardError with a carriage return and line feed on Windows ("curl: (22) The requested URL returned error: 404\r\n"); the pattern accepts either line ending.
5. Needs the author (program finding, still open): curl-mcp refuses the null device as an -o target (`-o NUL` answers PathRefused, "cannot be resolved"; `-o /dev/null` "outside CURL_MCP_ROOT"). The status-code case therefore uses `-s -w` without `-o`, which is honest only because the 404 body is empty.
6. The PathRefused text names absolute paths, so that case matches it with a pattern.
Re-pinned to Surl a423807a5d / Curl 0680b97577: the 404 now carries the body 404 Not Found, before -w's 404 in standardOutput (BL-1444).
