<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: sftp-find-why-a-key-login-failed-from-an-agent
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): Surl v0.1.5 (d64faa4bb), Curl 2145a49eae, dotnet 11.0.100, ModelContextProtocol.Core: all 5 cases pass on windows (verify.py --no-write).
## Findings
1. curl does offer an unauthorized key (curl -v and surl -v show the unsigned publickey query), but surl publishes no login_judged for that query (Surl decision, ADR-0128 d.2), so only a signed, refused key (public key authorized, private key not its pair) shows a refused publickey event. Not a Curl finding.
2. A wrong key the server was never given, a server with no keys and a password-only job leave the same record; stated in the Discussion and shown by cases.
3. Refused logins take about a second each; waits are bounded at 15 s. Windows only run.
