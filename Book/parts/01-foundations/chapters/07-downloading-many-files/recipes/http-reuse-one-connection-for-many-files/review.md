<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-reuse-one-connection-for-many-files
Verdict: accept
Checked against local builds on 2026-11-21 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port (Curl commit 7d167fa10a): 9 of 9 cases pass on windows.
## Findings
1. Log checks match the exchange number with a back-reference, so verify's ready probe (exchange 1) does not change the claim.
2. The idle-timeout case relies on a 2 s pause against `--idle-timeout 1`, a 1 s margin either side.
