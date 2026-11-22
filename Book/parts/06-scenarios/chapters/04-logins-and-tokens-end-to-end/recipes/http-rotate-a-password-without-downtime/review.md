<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-rotate-a-password-without-downtime

Verdict: accept

Checked against local builds on 2026-11-20 (not releases): surl 1.0.0 (Surl commit 9ad60d80c), Curl port 8.22.0 (Curl commit 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0, surl-mcp 1.0.0, curl-mcp 0.1.0.0: 8 of 8 cases pass on windows.

## Findings

1. surl holds one password per account, so the rotation is two accounts (app-v1, app-v2) with an overlap; both schemes share the account book. Changing a password in place and removing the last account are cases, as the two shortcuts that break it.
2. Control endpoint `POST /v1/accounts` for an existing name is now 409 (`user app already has an account`), as Surl.Api and surl-mcp refuse it; `PUT /v1/accounts/{user}` sets a password (fixed in Surl a423807a5d).
3. Removing the last account through the control endpoint opens the server (GET with no login 200) and now warns `surl: warning: no account left; the server now needs no login` (fixed in Surl a423807a5d).
4. The generated solution listing guesses the control listener's line as `Listening on http://127.0.0.1:8082`; surl prints `Control endpoint listening on ...` after the others (build.py, not the recipe).
5. FTP uses explicit TLS (`--ssl-reqd`), so the password never crosses in clear and no loosening option is needed.
6. Re-pinned to Surl a423807a5d / Curl 0680b97577: POST of an existing account is now 409 Conflict (BL-1490); the in-place change uses PUT /v1/accounts/app; the duplicate POST is a new case. Removing the last account now warns `no account left` (BL-1477).
