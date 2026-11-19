<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: http-add-an-account-from-an-agent
Verdict: accept with fixes
Checked against local builds on 2026-11-18 (not releases): surl 1.0.0 (Surl commit a0236e175), Curl port 8.22.0 (Curl commit f4b30d981f), surl-mcp 1.0.0, curl-mcp 0.1.0.0: 9 of 9 cases pass on windows.
## Findings
1. Fixed: "to change a password, remove the account and add it again" had no case; added `change-password`.
2. For the author (design, not a defect by surl's own rules): removing the last account with `remove_account` reopens the server to anonymous requests (case `last-account-opens`), and `add_account` on an open server closes it at once (`first-account-closes`). Consistent with `--user` on the console, but an agent cleaning up after a test can silently turn a login-only server into an open one. The Discussion says so and suggests keeping one account from `start_server`. Worth an ADR note or a warning in the tool's answer.
3. The server listens on plain HTTP and the client uses `--digest`, so no loosening option is needed. The Basic refusal (403) is a case here, but `--allow-plaintext-auth` is not named; <<http-refuse-a-password-in-clear>> covers it.
4. Status codes are read with `-s -w "%{http_code}"` and no `-o`, since curl-mcp refuses the null device (known finding); honest because 401 and 403 bodies are empty and the 200 body is asserted in full.
5. Error texts are matched by substring: `remove_account` on a missing account answers `server <id> has no account bob`, matched as `has no account bob` because verify does not substitute saved values into `error-contains`.
6. Not covered: `add_account` with an empty `user` or `password` (the schema's `minLength: 1` would fail the call before sending), and the `login_judged` event an account login produces (`get_events` shows `account`, `mechanism`, `is_accepted`); either could be a later recipe.
