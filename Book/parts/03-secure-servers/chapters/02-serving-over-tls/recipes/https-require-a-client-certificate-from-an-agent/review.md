<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-require-a-client-certificate-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 8 of 8 cases pass on windows; 1 case(s) for Linux and macOS only, not run here.
## Findings
1. curl-mcp transfer has no client-certificate field, so the curl tool is used. Refused connections show as exchange_ended with received_byte_count 0 (surl-mcp has no handshake-failure event or log after start).
