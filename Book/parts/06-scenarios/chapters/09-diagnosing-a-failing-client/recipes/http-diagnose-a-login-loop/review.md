<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-diagnose-a-login-loop
Verdict: accept
Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl v0.1.5, commit d64faa4bb), Curl port 8.22.0 (Curl commit 2145a49eae), surl-mcp 1.0.0, curl-mcp, dotnet 11.0.100, ModelContextProtocol.Core: 6 of 6 cases pass on windows.
## Findings
1. Wrong user and wrong password are refused alike by surl; the agent tells them apart only with its own list of accounts, which the Discussion says.
2. NTLM against a default server and a client with no -u leave no login_judged event; the exchanges (Authorization sent, WWW-Authenticate offered) tell them apart.
3. The loop is curl -f --retry 2 --retry-all-errors: three connections each ending 401. The record is read after polling exchange_started = exchange_ended (bounded, 10 s).
