<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-run-curl-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl-mcp 1.0.0 (Surl commit 3c5b39474), curl-mcp 0.1.0.0 (Curl commit b140f6d88c): 7 of 7 cases pass on windows.
## Findings
1. Needs the author (program finding): curl-mcp refuses the null device as an -o target. `{"args":["-s","-o","NUL","-w","%{http_code}",URL]}` answers PathRefused ("-o 'NUL' cannot be resolved"), and `-o /dev/null` answers PathRefused ("outside CURL_MCP_ROOT"). Upstream curl accepts both. The status-code case therefore uses `-s -w` without `-o`, which is honest only because the 404 body is empty; the Discussion says so. Once the null device is allowed, use the console idiom `-s -o {{null}} -w "%{http_code}"` instead.
2. Fixed: "an agent may reach the network freely" had no case, so it now reads "reaches the disk only where...".
3. The `-o` case writes hello.txt into the recipe's root. That file is in the temporary folder, so it is harmless.
4. Re-verified after the verify.py byte-exact fixtures fix: no claim here depends on fixture bytes (fixtures: {}; content goes in through start_server's files), so nothing changed.
