<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: redis-seed-a-cache-for-a-test-in-csharp

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 5 of 5 cases pass on windows (C#).

## Findings

1. Twin of <<redis-seed-a-cache-for-a-test>>: carries its claims as far as the C# surface can show them, each in its own words; see that recipe's review for the findings that apply to all three.
2. Timing: the expiring key is set and read in one curl run (PX 3000, sleep 4); the C# and agent twins make the first Curl.Api / curl-mcp request before any short expiry is set, then use 4 s and wait 5 s.
