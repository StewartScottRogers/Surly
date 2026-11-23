<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-fault-until-a-client-retries-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl v0.1.5 (d64faa4bb), Curl 2145a49eae, dotnet 11.0.100, ModelContextProtocol.Core: all 6 cases pass on windows (verify.py --no-write).
## Findings
1. The client call is started and not awaited while wait_for_event waits (10 s bound); rule uses are claimed as 1 to 5, not an exact count.
2. fault_applied events carry rule_id and target (the path); kept rule gives exit 22 and three events with --retry 2.
