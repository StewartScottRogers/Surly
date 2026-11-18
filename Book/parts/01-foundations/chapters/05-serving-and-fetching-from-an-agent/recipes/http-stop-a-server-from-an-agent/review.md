<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-stop-a-server-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl-mcp 1.0.0 (Surl commit 3c5b39474), curl-mcp 0.1.0.0 (Curl commit b140f6d88c): 5 of 5 cases pass on windows.
## Findings
1. Fixed: "never the old ID", "as surl exits on Ctrl+C" and the list of every list_servers field had no case, so I removed or trimmed them.
2. The claim that list_servers no longer shows a stopped server cannot be a case, because result-contains cannot prove absence. The Discussion therefore shows it through the stop-twice and get_exchanges refusals instead.
3. Only exit code 0 from stop_server is claimed. No way was found to make a clean stop answer anything else.
4. <<surl-read-the-exit-codes>> is a forward reference to a chapter 3 recipe that is not written yet.
5. Re-verified after the verify.py byte-exact fixtures fix: no claim here depends on fixture bytes (fixtures: {}; content goes in through start_server's files), so nothing changed.
