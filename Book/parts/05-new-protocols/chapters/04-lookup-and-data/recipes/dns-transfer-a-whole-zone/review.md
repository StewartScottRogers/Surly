<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: dns-transfer-a-whole-zone

Verdict: accept

Checked against local builds on 2026-11-22 (not releases): Surl 5da5b3eaf, Curl c84035f797, dotnet 11.0.100: 7 of 7 cases pass on windows.

## Findings
1. No `--allow-zone-transfer` stderr warning appears on start (probed), unlike other loosening options; the Discussion does not claim one.
2. curl writes NOTAUTH as RCODE9 (it has no name for code 9); the claim says so.
3. Two servers cannot share a --directory, so the closed server has its own zone copy.
