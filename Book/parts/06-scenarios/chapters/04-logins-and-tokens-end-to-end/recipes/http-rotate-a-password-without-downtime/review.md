<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-password-without-downtime

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 8 of 8 cases pass on windows.

## Findings

1. surl holds one password per account, so the rotation is two accounts (app-v1, app-v2) with an overlap; both schemes share the account book. Changing a password in place and removing the last account are cases, as the two shortcuts that break it.
2. Control endpoint `POST /v1/accounts` for an existing name replaces its password (204), while Surl.Api `Accounts.Add` throws ArgumentException and surl-mcp `add_account` is a tool error (`user app already has an account`): three surfaces, two behaviours. Reported.
3. Removing the last account through the control endpoint opens the server (GET with no login 200) and writes no warning to the log, where Surl.Api's `Accounts.Remove` logs `no account left` and surl-mcp's answer carries a warning. Reported.
4. The generated solution listing guesses the control listener's line as `Listening on http://127.0.0.1:8082`; surl prints `Control endpoint listening on ...` after the others (build.py, not the recipe).
5. FTP uses explicit TLS (`--ssl-reqd`), so the password never crosses in clear and no loosening option is needed.
