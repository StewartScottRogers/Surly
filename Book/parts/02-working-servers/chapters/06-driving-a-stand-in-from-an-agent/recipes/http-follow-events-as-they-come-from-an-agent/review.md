<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-follow-events-as-they-come-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl v0.1.5 (d64faa4bb), Curl 2145a49eae, dotnet 11.0.100, ModelContextProtocol.Core: all 6 cases pass on windows (verify.py --no-write).
## Findings
1. Sequence numbers are not consecutive (an exchange makes several, kept or not), so the recipe claims increasing and identical to the uncapped read, not +1 steps.
2. Each request is followed by a bounded wait for its exchange_ended so event order is stable (S E S E ...).
