<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: grpc-test-a-failing-call-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, dotnet 11.0.100, ModelContextProtocol.Core 2.2.0: 6 of 6 cases pass on windows.
## Findings
1. Every failing status is a tool error with `isError` true and curl's exit code 22; exit 28 for `-m` is also a tool error. For the stream that fails part way the output holds the three replies and the status is on standard error.
2. The program prints the status from the first `grpc-status` line of standard error; a Windows carriage return there is trimmed.
