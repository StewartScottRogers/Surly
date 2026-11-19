<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: ftp-protect-the-login-with-tls
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f): 11 of 11 cases pass on windows.
## Findings
1. Truth (7): the default refusal (530 "Login needs TLS first", right and wrong password alike, exit 67) and the loosening `--allow-plaintext-auth` (right password logs in, wrong one 67) are both cases, the latter against a second `surl` on port 8082 with its own directory (`plain`), since two `surl` processes cannot share one.
2. Fixed: the prose first said the second `surl` was "in the listing"; the build lists only the solution's server, so the Solution text now prints the second command itself.
3. Reader: `curl --ssl-reqd` sends `AUTH SSL` first and `surl` accepts it (234); the prose says "an `AUTH` command" and quotes surl's own 530 text, which names AUTH TLS. `--ssl` falling back to clear is cased, so the advice to use `--ssl-reqd` is a checked claim.
4. Truth (4): "`--self-signed` and `curl -k` can stand in" is a pointer to the self-signed recipe, not a case here.
5. Uses `rfc4217` (FTP over TLS), already in the bibliography.
