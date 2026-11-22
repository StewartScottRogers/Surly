<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: webtransport-send-datagrams-and-streams
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), Surly.Surl.Api 1.0.0, Surly.Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100: 10 of 10 cases pass on windows.
## Findings
1. Datagram limit found by probing: 1,099 bytes of payload go through, 1,100 are refused by curl before sending, exit 55, `at most 1100 bytes, quarter stream ID included` (the limit counts the one-byte quarter stream ID). Not seen on Linux or macOS; CI will tell.
2. Payloads of exact size come from `openssl rand` in setup, sent with --data-binary; -w %{size_upload} %{size_download} counts them.
3. Every curl has -m 10. An unknown wt= mode is exit 3 before anything is sent.
