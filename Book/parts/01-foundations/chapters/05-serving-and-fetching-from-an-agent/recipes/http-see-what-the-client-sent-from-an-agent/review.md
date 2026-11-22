<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-see-what-the-client-sent-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. Rewritten from an mcp: call list to a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19). One program, read-exchanges.cs, with five scenarios chosen by argument; every earlier claim is still a case. It prints each exchange's first and last chunk with the data as JSON spells it, so the bytes, carriage returns included, are compared exactly.
2. Fixed: the server now listens on port 0, as an agent's would. The fixed {{port}} is gone (program cases do not substitute placeholders in expectations); each pattern captures the bound port and requires the Host line to name the same one. The old build.py finding about a literal {{port}} in the listing no longer applies here.
3. Fixed: the filter case now proves the answer is exactly exchanges 1 and 2 (the pattern is anchored), and the page case that it is exactly exchange 1, not merely that they are included.
4. For the author: with a C# program, an empty answer can now be claimed (for example, scheme https on this http-only server keeping nothing). Not added, to keep the claims as they were.
5. Earlier findings stand: max_exchanges' default and transfer's userAgent field are not claimed; the second client uses -A surly-reader so the exact bytes do not change with Curl's version.
6. Bounded wait before reading the server's record (CI race sweep).
