<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-retry-through-a-busy-server-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), Curl.Api 0.2.0, Surl.Api 1.0.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0, ModelContextProtocol.Core 2.2.0: 5 cases pass on windows.
## Findings
1. Warm-up request to /health.txt before the Stopwatch; the timing claim is a range (2 to 20 s).
2. The server log (Log = StringWriter, LogLevel.Verbose) counts requests per path; RecordExchanges gives the FaultApplied events and Faults.List() the remaining uses.
