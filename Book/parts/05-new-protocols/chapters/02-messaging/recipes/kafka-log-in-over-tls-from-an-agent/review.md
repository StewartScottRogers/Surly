<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: kafka-log-in-over-tls-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 9 cases pass on windows.
## Findings
1. Same claims as the console recipe; produce and consume are curl-mcp calls and `read_log` shows what the login let through.
2. Text is printed only for a login refusal (exit 67); a handshake refusal's text is the TLS library's.
