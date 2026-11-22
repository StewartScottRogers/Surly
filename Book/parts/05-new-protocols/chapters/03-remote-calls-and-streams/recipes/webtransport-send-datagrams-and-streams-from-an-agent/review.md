<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: webtransport-send-datagrams-and-streams-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 9 of 9 cases pass on windows.
## Findings
1. Payloads for the size cases go in as the curl tool's stdin with --data-binary @-, so no file is written; totalBytes counts the echo.
2. wait_for_event answers {"event": {..., "account", "is_accepted"}}; used for the wrong-password login.
