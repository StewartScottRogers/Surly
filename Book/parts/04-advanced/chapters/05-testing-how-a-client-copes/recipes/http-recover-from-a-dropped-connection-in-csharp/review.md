<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-recover-from-a-dropped-connection-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-19 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 3 of 3 cases pass on windows.
## Findings
1. Curl.Api showed no retry option, so the program retries itself; the prose says so without claiming the library has none.
2. Both sides count: client retries, server Exchanges.Count, FaultApplied events and the rule's Remaining.
