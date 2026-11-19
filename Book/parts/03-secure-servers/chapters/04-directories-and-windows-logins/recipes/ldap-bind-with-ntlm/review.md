<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ldap-bind-with-ntlm
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 7 of 7 cases pass on windows.
## Findings
1. Truth (4, 7): the default refusal of NTLM (`authMethodNotSupported`, exit 38) and the `--auth negotiate` behaviour are cases against two more `surl`, each with an empty directory; their cases read the root DSE, which needs no entries.
2. Truth (4): `--auth negotiate` does not accept `curl --ntlm` (WinLDAP's Sicily bind), only `--negotiate` (`GSS-SPNEGO`). `surl --aihelp ldap` says the NTLM binds are "accepted with `--auth ntlm` or `--auth negotiate`", which reads as either word accepting either option; the build does not, and the recipe states what it does. Worth tightening the help text upstream (not a defect: the ADR-0072 table maps Sicily to `ntlm`).
3. Truth (5): that `WinLDAP` seals every message after the bind is cited to <<surl-readme>> ("no password then crosses the wire in clear") and inferred from the search answering; it is not observed directly.
4. Platform: Windows only. The binds are `WinLDAP`'s; the prose says the recipe makes no claim about curl on Linux or macOS. Verify there before publishing.
5. Removed a sentence claiming `--auth ntlm,negotiate` accepts both, which had no case.
