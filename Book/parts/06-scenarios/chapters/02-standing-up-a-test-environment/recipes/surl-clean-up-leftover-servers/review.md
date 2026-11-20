<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-clean-up-leftover-servers
Verdict: accept with fixes
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. The leftovers are made by the program itself (three start_server calls) since servers do not outlive their surl-mcp host; that is a case (host-exit-stops-servers), and so is a second host listing none of the first host's servers.
2. Failure paths: fetch after clean-up (exit 7), stop of an already stopped ID (tool error).
