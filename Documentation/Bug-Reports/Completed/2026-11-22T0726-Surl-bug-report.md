<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-22 (batch 10 of the 1,000-recipe plan)

From writing and verifying batch 10 (60 recipes: messaging scenarios, MQTT 5, NATS, XMPP, FTP diagnosis, HTTP diagnosis, gRPC streams) of the *Surly* cookbook against Surl `5da5b3eaf` (master), Curl `c84035f797`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | **High** | NATS auth | A token login is never accepted: `--user :tok` refuses `curl -u tok:`, a raw `CONNECT {"auth_token":"tok"}` and `{"user":"","pass":"tok"}`, over `nats://` and `natss://`, with or without `--allow-plaintext-auth` (exit 67, `Authorization Violation`, log reason `RefusedCredentials`). User/password works; the same `:tok` account works as an HTTP Bearer token |
| 2 | Medium | Surl.Api | `FindExchanges` with `SurlEventQuery.Name` set ignores the name, by the source (`ExchangeFacts.Matches`); `FindEvents` honours it. Not yet tested, please confirm |
| 3 | Low | SSH events | A refused SFTP public-key login publishes no `login_judged` event (BL-1474 adds a `-v` note; the event is still missing), so a test sees the refusal only through curl's 67 |
| 4 | Low | Logging | With two listen URLs of one scheme, the `-v` log and `--trace-ascii` name the scheme and client per exchange (`#N * Exchange N opened: http from <client>`) but never the listener; the control endpoint's `exchange_started` events do |
| 5 | Low | `surl-mcp` | `list_messages` and `read_message` with `owner: ""` (the `--allow-anonymous` mailbox's owner) are refused as invalid params, so an agent cannot read that mailbox |

## Repros

**1.** `surl --user :tok --allow-plaintext-auth nats://127.0.0.1:4222/`, then `curl -u tok: -d hi nats://127.0.0.1:4222/orders` → exit 67. Over TLS: `surl --cert server.pem --key server.key --user :tok natss://127.0.0.1:4443/` and `curl --cacert server.pem --ssl-no-revoke -u tok: -d hi natss://127.0.0.1:4443/orders` → 67. `--user alice:pw` with `curl -u alice:pw` → 0. Recipe: `nats-log-in-with-a-token` (its last case records the refusal and will fail once fixed).

**2.** In C#: `server.FindExchanges(new SurlEventQuery { Account = "app", Name = "/inbox/a.txt" })` after two uploads by `app` to different paths; per `ExchangeFacts.Matches`, both come back. Recipe `ftp-find-which-account-uploaded-a-file-in-csharp` uses `FindEvents` instead.

**3.** `surl --authorized-keys deploy:deploy.pub --hostkey host_key sftp://127.0.0.1:2222/`, then `curl --key other_key -u deploy: sftp://127.0.0.1:2222/` (67); `get_events` has no `login_judged`.

**4.** `surl -v --directory site http://127.0.0.1:8080/ http://127.0.0.1:8081/`, fetch from each: both log lines read `Exchange N opened: http from 127.0.0.1:...`.

**5.** surl-mcp `start_server` with `--allow-anonymous` and SMTP+POP3, send mail to it, then `list_messages` with `{"owner": ""}` → invalid params.
