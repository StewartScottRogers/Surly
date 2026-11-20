<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-run-a-fixture-from-a-config-file-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.
## Findings
1. Nearest honest recipe: surl-mcp's start_server refuses --config/-K (`a config file could name paths this host never checks`), so the agent reads the file itself into listen_urls and arguments. The refusal is a case; it is by design, not reported as a bug.
2. The program's reader handles only what the file uses (no escapes inside quotes); the prose says so.
3. Paths read from the file are confined: directory = ../elsewhere is refused (case outside-root).
