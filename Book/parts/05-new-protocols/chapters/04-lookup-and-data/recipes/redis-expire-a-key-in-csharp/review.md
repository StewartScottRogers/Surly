<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-expire-a-key-in-csharp

Verdict: accept with fixes

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0, ModelContextProtocol.Core 2.2.0: 4 of 4 cases pass on windows.

## Findings
1. The reads before the expiry run under a one-minute expiry, and the key is then set again to one second and read after two: the first Curl.Api request in a cold process took 4 s on an idle machine, so a six-second expiry was read as null under load.
2. Surl.Api's expiry bounds (1 ms to 365 days) are the README's; the cases show zero and 366 days refused and 1 ms accepted.
Re-pinned to Surl 9ad60d80c / Curl 7d167fa10a: the solution read null before expiry under load, not a Curl regression (a warm run reads "abc" with TTL still 5 s; a cold first request took 4 s); the expiry is now a minute for the reads and one second, waited out for two, for the gone key.
