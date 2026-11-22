<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: https-log-in-with-tls-srp-from-an-agent
Verdict: accept with findings (nearest honest recipe)
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf), Curl port 8.22.0 (Curl commit c84035f797), dotnet 11.0.100, Surly.* packages from the batch feed: 7 of 7 cases pass on windows.
## Findings
1. As the console twin: curl-mcp's curl tool gives exitCode 35 with the three 'no function' warnings; transfer has no SRP field. No login is claimed.
2. Adds add/set/remove_tls_srp_account answers and errors; error texts are the tools' own ('it holds ':' or already has a TLS-SRP account', 'user nobody has no account', 'server s1 has no TLS-SRP account device', 'server s1 was not started with TLS-SRP').
