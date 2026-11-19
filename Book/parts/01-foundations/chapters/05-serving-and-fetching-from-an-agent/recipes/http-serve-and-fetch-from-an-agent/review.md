<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-serve-and-fetch-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, serve-and-fetch.cs, with six scenarios chosen by argument; every earlier claim is still a case. The program prints whether the call it reports was a tool error and exits 1 if it was.
2. Fixed: the solution now also checks that `failure` is null and that `start_server` echoes `listen_urls` as given, both said in the Discussion; the port-in-use case now checks surl's own message as well as 45 (BindFailed); the file-URL case checks the whole failure text.
3. Fixed: JavaScript Object Notation (JSON) and software development kit (SDK) are spelled out on first use.
4. The Discussion says why the hosts' roots are environment variables (the SDK starts a host through cmd.exe on Windows, which breaks on a path with a space). That is from build/README.md and the template, not a case here.
5. The solution's output contains the bound port, so its expectation is a pattern, as in chapter 4's in-process recipe.
6. Earlier findings stand: files never on disk, the first ID being s1 and header order are not claimed; exit code 45 agrees with `surl --aihelp exit-codes`.
