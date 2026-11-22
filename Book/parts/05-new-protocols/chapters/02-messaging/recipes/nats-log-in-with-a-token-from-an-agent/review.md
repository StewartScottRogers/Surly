<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: nats-log-in-with-a-token-from-an-agent

Verdict: needs-work (program defect: a token login is never accepted)

Checked against local builds on 2026-11-22 (not releases): surl-mcp 1.0.0 (Surl commit 5da5b3eaf) and curl-mcp (Curl commit c84035f797): 12 of 12 cases pass on windows.

## Findings

1. Same defect as the console recipe. surl-mcp refuses --log-file, so the agent cannot read the log: the two log claims become one claim from get_exchanges (token in auth_token, no user or pass) and one that the record holds the token in clear, unlike the log. The refusal reasons are shown with get_events login_judged: none for a login refused in clear, one when judged.
- CI (Linux): curl's natss:// TLS handshake fails there (35, wrong version number: the port starts TLS before reading NATS's plain INFO line); the natss refusal cases are marked Windows and macOS; reported to Curl.
- CI (Linux): every case that needs a natss handshake (token-is-sent-as-auth-token, record-holds-the-token, right-token-refused) is marked Windows and macOS until Curl BL-5750.
