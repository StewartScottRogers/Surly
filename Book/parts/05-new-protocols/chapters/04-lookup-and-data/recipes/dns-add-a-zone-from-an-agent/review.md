<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-add-a-zone-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 71807f05f), Curl port 8.22.0 (Curl commit 82913be53d), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 7 of 7 cases pass on windows.
## Findings
1. Written as a C# program on the MCP SDK (ADR-0008 item 7, amended 2026-11-19), one program, `zones.cs`, its scenario chosen by argument.
2. Program finding, left out of the recipe: curl-mcp's `curl` tool refuses the Curl port's own `dns` options, `--dns-json` and `--dns-transport`, before running curl: `{"failure":{"kind":"PathRefused","text":"Refused --dns-json: --dns-json is not an option this server knows, so it cannot tell whether it uses a local path."}}`. So the program parses curl's dig-like text with a regular expression instead of asking for JSON, and the TCP transport is not shown here. Reported to the lead.
3. Reader: curl's header line carries a random message ID; the program prints only the status and the answer section's records, so every case is an exact `stdout`. The Discussion says what it leaves out.
4. The zone text is built from `\n`-terminated strings rather than a raw string literal, so `byte_count` (117) does not depend on the line endings a checkout gives the source file.
5. Observed, not claimed: a zone whose `$ORIGIN` is outside its apex is refused (`line 3: an owner outside the zone`), and `add_dns_zone` succeeds on a server with no `dns` listener.
