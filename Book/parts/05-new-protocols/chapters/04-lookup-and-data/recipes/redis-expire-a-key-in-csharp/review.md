<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-expire-a-key-in-csharp

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.

## Findings
1. The expiry is six seconds and the wait seven: the first Curl.Api request takes over a second to start, and an earlier four-second version read null before the value.
2. Surl.Api's expiry bounds (1 ms to 365 days) are the README's; the cases show zero and 366 days refused and 1 ms accepted.
