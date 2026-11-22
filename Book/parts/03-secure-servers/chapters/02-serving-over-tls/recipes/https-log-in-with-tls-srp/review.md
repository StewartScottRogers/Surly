<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-log-in-with-tls-srp
Verdict: accept with findings (nearest honest recipe)
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 8 of 8 cases pass on windows.
## Findings
1. Planned task not possible with these builds: no client here speaks TLS-SRP. curl 8.22.0 (and this port, Curl ADR-0480) dropped it; --tlsauthtype/--tlsuser/--tlspassword warn 'has no function' and the handshake fails 35. So the right-password login and the wrong-password refusal (bad_record_mac per surl --aihelp tls) are not claimed; the recipe shows the server side: no certificate, refusal of a non-SRP client with the server's reason, and the start-up refusals.
2. openssl s_client -srpuser/-srppass was tried as a stand-in client: MSYS2 ucrt64 OpenSSL 3.5.9 segfaults (exit 139), Git for Windows OpenSSL 3.5.7 stalls after ClientKeyExchange, and it failed against openssl s_server SRP too, so it is not used. Linux (Ubuntu 26.04, OpenSSL 3.5.5 with SRP) was not reachable from WSL here; a [linux] s_client case could be added once probed in CI.
3. Problem rewritten from the plan row to what the recipe can show (holds the account, needs no certificate, refuses a client that cannot log in).
4. The wrong-password behaviour is cited to surl-readme, not claimed. RFC 5054 is not in the bibliography.
