<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-with-your-own-certificate
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 7 of 7 cases pass on windows.
## Findings
1. Fitness (3): the certificate and key are made by `setup:` (openssl), never committed. The Solution listing prints the openssl line first, so a reader runs exactly what was tested.
2. Truth (4): every refusal is a case: no certificate (58), `--cert` without `--key` when the PEM holds no key (58), a missing `--cert` file (58), `--key` without `--cert` (2), `curl` without `--cacert` (60), and a name the certificate does not hold, `localhost` (60).
3. Truth (5), fixed: removed "surl writes no warning" (not observable by verify) and softened the paragraph on authority-issued certificates to advice with a citation; nothing here is cased for a real authority.
4. Not covered, left for later: a `--cert` file holding both certificate and key (no portable way to concatenate in `setup:` across sh and PowerShell), `--pass`, `--cert-type`.
