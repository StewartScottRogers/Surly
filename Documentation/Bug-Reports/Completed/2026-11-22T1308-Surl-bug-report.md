<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-22 (batch 12 of the 1,000-recipe plan)

From writing and verifying batch 12 (62 recipes: mail app tests, MQTT readings, logins over TLS, TLS-SRP, client certificates, proxies, LDAP provisioning, expiring tokens, read-back singles) of the *Surly* cookbook against Surl `5da5b3eaf`, Curl `c84035f797`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Low | `--mailbox-file` | A protocols cell naming a scheme with no login is refused without the file and line, unlike every other refusal in that file |
| 2 | Question | Accounts at run time | A Bearer-token account (`--user :tok`) cannot be added, replaced or removed while the server runs: control and surl-mcp refuse an empty user name. Intended? |
| 3 | Question | STOMP queues | A STOMP `SEND` to an undeclared queue creates it silently, while `EnqueueAsync` to an undeclared queue is refused. Intended? |

## Repros

**1.** Re-run 2026-11-22.
- `f.csv`: `address,password,protocols` / `ann@example.com,annpass,gopher`
- `surl --mail-domain example.com --mailbox-file f.csv smtp://127.0.0.1:0/`
- Observed: `surl: (2) scheme gopher has no login`.
- Expected: `surl: (2) Mailbox file f.csv, line 2: scheme gopher has no login`, as `nosuch` gets `Mailbox file g.csv, line 2: unknown scheme nosuch`.
- Recipe `smtp-load-mailboxes-from-a-csv-file` (review.md finding 1).

**2.** `surl --user :tok123 https://127.0.0.1:8443/` with the control endpoint.
- `POST /v1/accounts` with `{"user":"","password":"tok456"}` → 400 `give user and password, neither empty`.
- `DELETE /v1/accounts/` → 404.
- surl-mcp `add_account` `{"user":"","password":"tok456"}` → `McpProtocolException`.
- A test that wants a token to expire mid-session can't do it on a plain HTTP account. Recipes `http-expire-a-token-mid-session*` use the OCI registry token endpoint instead.

**3.** `surl --queue jobs stomp://127.0.0.1:61613/`.
- STOMP `SEND destination:/queue/other` → accepted; `other` now exists.
- Surl.Api `EnqueueAsync("other", ...)` on an undeclared queue → refused.
- Recipe `amqp-check-what-a-queue-holds-in-process`, case `wrong-queue`.
