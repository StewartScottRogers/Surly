<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-inject-a-fault-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 993aa7072), Curl port 8.22.0 (Curl commit 0afbd10e3b), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows (three runs).

## Findings

1. Written as a C# program on the MCP SDK from the start (ADR-0008 item 7, amended 2026-11-19); one program, `faults.cs`, its scenario chosen by argument, a fresh server per run.
2. Fault IDs (`f1`, `f9`) and the server ID `s1` are printed as they are: each run starts a fresh host, and IDs are counted per server.
3. `get_events` is in `mcp-tools` and the Discussion for the `events` claim, which is how an agent checks the fault it planned is the one the client met.
4. The `item` that `list_faults` answers is `surl`'s own spelling, keys reordered and percent-encoded; the claims match it exactly.
5. Removed in drafting: "there is no tool that clears every rule" is in the Surl.Mcp README (ADR-0161), not the repository README the bibliography's `surl-readme` names, and no case can prove a tool's absence.
