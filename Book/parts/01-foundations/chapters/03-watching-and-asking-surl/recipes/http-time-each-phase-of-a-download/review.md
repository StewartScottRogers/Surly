<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-time-each-phase-of-a-download
Verdict: accept
Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 763754785), Curl port 8.22.0 (Curl commit b9ca725077): 4 of 4 cases pass on windows.
## Findings
1. No exact times claimed: ranges (connect < 1 s, starttransfer >= 1.5 s behind a 1.5 s delay fault) and zeros (appconnect over http, connect on a reused connection).
2. TLS case uses a certificate from setup; TLS 1.2 or 1.3 both give appconnect > 0, so no platform split.
