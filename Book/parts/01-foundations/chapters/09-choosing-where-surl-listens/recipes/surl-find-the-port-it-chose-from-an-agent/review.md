<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: surl-find-the-port-it-chose-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 5 of 5 cases pass on windows.
## Findings
1. The console twin's failure path (an address the machine lacks, exit 45) cannot be shown here: surl-mcp refuses any non-loopback host, 192.0.2.1 included, before binding. The nearest surl refusal, a path in the listen URL (3, MalformedUrl), is used instead; the port-in-use 45 is in http-serve-and-fetch-from-an-agent.
2. start_server also answers log_lines (the Listening on lines); not used or claimed.
