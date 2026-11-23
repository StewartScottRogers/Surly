<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-diagnose-a-404-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl v0.1.5 (d64faa4bb), Curl 2145a49eae, dotnet 11.0.100, ModelContextProtocol.Core: all 5 cases pass on windows (verify.py --no-write).
## Findings
1. Request and status lines are paired in order from get_exchanges received and sent chunks; each transfer is one exchange.
2. The store is case-sensitive: /Reports/... is a 404 and list_files shows reports.
