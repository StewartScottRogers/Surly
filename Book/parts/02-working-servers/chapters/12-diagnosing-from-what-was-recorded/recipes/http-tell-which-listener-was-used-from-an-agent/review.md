<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-tell-which-listener-was-used-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 5 of 5 cases pass on windows.
## Findings
1. `get_events` exchange_started carries `listen_url`; `get_exchanges(listen_url)` filters. The Host-lying client uses the curl tool. FTP via transfer answers statusCode 226.
