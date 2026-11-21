<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-give-up-within-a-time-budget-in-csharp
Verdict: drafted, ready for review
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a), dotnet 11.0.100, Surl.Api 1.0.0, Curl.Api 0.2.0: 8 of 8 cases pass on windows, one claim per case (a label argument prints one line).
## Findings
1. Warm-up request before the rules are added (server.Faults.Add) and before the Stopwatch.
2. The loop applies curl's --retry-max-time rule itself; a new CurlClient per try so each try connects (and is held) again.
3. Times and counts as ranges; hang scenario bounded by CurlClientOptions.Timeout.
