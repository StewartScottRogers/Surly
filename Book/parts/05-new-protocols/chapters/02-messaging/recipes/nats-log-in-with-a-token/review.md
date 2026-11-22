<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# Review: nats-log-in-with-a-token

Verdict: needs-work (program defect: a token login is never accepted)

Checked against local builds on 2026-11-22 (not releases): surl 1.0.0 (Surl commit 5da5b3eaf) and Curl commit c84035f797: 12 of 12 cases pass on windows.

## Findings

1. Program defect: surl nats/natss refuses every auth_token login, the right token included (log reason RefusedCredentials, curl 67 Authorization Violation), with --user :tok123, --user-file, over nats with --allow-plaintext-auth and over natss. The same account works as an HTTP Bearer token. So the planned claim 'accepted over NATSS' cannot be shown; the recipe covers what works (token sent as auth_token, refused in clear with RefusedPlaintext, judged under the loosening and over natss, masked in the log) and records the defect in its last case, which will fail when it is fixed.
2. Windows curl needs --ssl-no-revoke with --cacert against natss (SEC_E_ILLEGAL_MESSAGE, exit 35, without it); mqtts and xmpps need no such option. Not seen on Linux or macOS.
3. The log checks use -v --log-file and a polling sh script (needs a POSIX shell).
- CI (Linux): curl's natss:// TLS handshake fails there (35, wrong version number: the port starts TLS before reading NATS's plain INFO line); the natss refusal cases are marked Windows and macOS; reported to Curl.
