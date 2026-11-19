<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-serve-with-a-self-signed-certificate
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 6 of 6 cases pass on windows.
## Findings
1. Truth (7): the loosening is shown both ways: exit 58 with no `--self-signed` and no `--cert` (a `surl` case), and the served file with it. The Discussion says what it gives up (no client can tell this server from another) and why it is not the default.
2. Truth (4): the start-up warning is quoted from the README and `--aihelp tls` and cited, not cased; verify cannot read a server's log, and `surl` has no run-once mode to capture it.
3. Fitness (1): `curl: (60)` wording comes from Schannel on Windows and differs with OpenSSL; the cases claim only the exit code and the `curl: (60)` prefix, and the prose says so.
4. Fixed: `-o {null}` in the prose became `-o /dev/null` (`NUL` on Windows), as Part 1 writes it. Part 1's link to this key now resolves.
