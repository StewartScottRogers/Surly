<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-retry-a-flaky-endpoint-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 4 of 4 cases pass on windows.
## Findings
1. Curl.Api has no retry option seen; the program retries itself on the statuses curl --retry counts transient, with a doubling wait.
2. Server side: FaultApplied events and the rule's Remaining (Exchanges.Count is not used: the client reuses one connection).
