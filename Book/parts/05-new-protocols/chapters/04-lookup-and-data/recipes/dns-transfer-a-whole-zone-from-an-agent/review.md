<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-transfer-a-whole-zone-from-an-agent

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797, dotnet 11.0.100: 7 of 7 cases pass on windows.

## Findings
1. curl tool answers nonzero exit (IXFR, 3) as a tool error; program exits 1 there and the claim says so.
