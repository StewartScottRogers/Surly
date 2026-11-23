<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-assert-a-clients-back-off-schedule
Verdict: accept
Checked against local builds on 2026-11-22: Surl v0.1.5 (d64faa4bb) and Curl 2145a49eae (the feed's Surl.Api 1.0.0 and Curl.Api 0.2.0 reuse those version numbers), dotnet 11.0.100: all cases pass on windows.
## Findings
1. Gaps read from SurlExchangeRecord.StartedAt, one connection per attempt; timing claims are lower bounds and a first-versus-last ordering.
- Lead: CI on macOS (batch 13) delayed the flat client's second gap past 450 ms, so only the third was short; flat-client now claims the third always and the second where the machine keeps time.
