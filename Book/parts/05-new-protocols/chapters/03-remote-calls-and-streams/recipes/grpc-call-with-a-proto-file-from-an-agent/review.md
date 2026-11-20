<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-call-with-a-proto-file-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 8 of 8 cases pass on windows.
## Findings
1. The `bytes` case compares the bytes surl sent for the two calls, from `get_events` (`exchange_ended`, `sent_byte_count`): by reflection the server also sends the descriptor, so more. Only the comparison is printed. It is the one honest trace of 'with the file, no reflection call' these tools give; `get_exchanges` returns HTTP/2 frames in base64 with compressed headers.
2. The `.proto` files are recipe fixtures in the folder both hosts are confined to.
