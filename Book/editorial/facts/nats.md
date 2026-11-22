<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# nats facts
Checked against: surl 1.0.0 (Surl 5da5b3eaf), curl (Curl c84035f797), 2026-11-22, windows. Written for nats-log-in-with-a-token (and twins); nats-publish-and-subscribe (older) has its own probes in its recipe.yml.

## Listen URLs and readiness
`nats` and `natss` (TLS from the first byte, or after the plain INFO that says `tls_required`); TCP, `ready: port`. `natss` needs `--cert`/`--key` or `--self-signed`. One bus with MQTT, STOMP, AMQP and Kafka.

## Server options that matter
- `--allow-anonymous` (loosens, warns): every CONNECT is accepted, a token or a bad login included (curl `-u tok123: -d paid nats://...` exit 0).
- `--user alice:s3cret` (user and password) and `--user :tok` (an account with an empty name: the NATS token, as an HTTP Bearer token is). `--user-file` with a `:tok` line is read the same way.
- `--allow-plaintext-auth` (loosens, warns): passwords (and tokens) over `nats://` are judged instead of refused unchecked.
- `-v --log-file f.log` writes the verbose log to a file (console and Surl.Api only; surl-mcp refuses `--log-file`: "surl-mcp writes each server's log to its own standard error").

## Secure by default
- No login, no `--allow-anonymous`: `-ERR 'Authorization Violation'`, curl exit 67 `curl: (67) NATS server refused the login: Authorization Violation`. The -v log says `CONNECT's login was refused (RefusedAnonymous)`.
- A password or token over `nats://` without `--allow-plaintext-auth`: the same error, log reason `RefusedPlaintext` (no `login_judged` event). With it, a wrong one is `RefusedCredentials` (a `login_judged` event).
- `nats://` to a `natss` listener: exit 35 `curl: (35) NATS server requires TLS; use natss://` (from the INFO), no CONNECT sent.
- `natss` with an untrusted certificate: exit 60.
- The -v log masks `pass` and `auth_token` as `****` in the CONNECT line (`#N < CONNECT {...,"auth_token":"****"}`); the token appears nowhere else. `--trace-ascii` wraps its dump at 64 bytes, so a pattern spanning a wrap (`auth_token`) never matches there: use `-v --log-file`.
- surl-mcp `get_exchanges` holds the CONNECT UNMASKED: `"auth_token":"wrong"` (to report; it is the test's own view, not the log).

## DEFECT (Surl 5da5b3eaf): a token login is never accepted
`--user :tok123` (also `--user-file`, Surl.Api `new SurlAccount("", "tok123")`), curl `-u tok123:` or `nats://tok123@host/` (CONNECT carries `auth_token`), over `nats://` with `--allow-plaintext-auth` or over `natss://`: refused, log `RefusedCredentials`. Raw CONNECT `{"auth_token":"tok"}`, `{"user":"","pass":"tok"}`, `{"user":"tok"}` are all refused, while `{"user":"bob","pass":"pw"}` against `--user bob:pw` is accepted. The same `--user :tok` account works as an HTTP `--oauth2-bearer tok` (404 for a missing file, 401 for a wrong token). The recipes' last case records the refusal; delete it and add the accepted case when fixed.

## Client: curl
`-u tok:` (user name, empty password) sends `auth_token` (no `user`/`pass`); `-u alice:pw` sends `user`/`pass`; `-u :tok` sends no credentials at all (RefusedAnonymous); `--oauth2-bearer` sends none either. `-d msg nats://h:p/subject` publishes (exit 0, no output).
- Windows curl with `--cacert server.pem` alone against `natss://`: exit 35 `SEC_E_ILLEGAL_MESSAGE` every time; `--ssl-no-revoke` makes it work (mqtts and xmpps need no such option). Without `--cacert`, exit 60. Not seen on Linux or macOS. Curl.Api: `CurlTlsPeerOptions { CertificateAuthorityFile, SslOptions = SslOptions.NoRevoke }` (`Curl.Api.Options.Tls`).

## MCP
`start_server(listen_urls, arguments)`; `get_events(server_id, kinds:["login_judged"])` has no event for a login refused unchecked and one for a judged login; wait with `wait_for_event(kinds:["exchange_ended"])` first.

## C#
`SurlServerOptions`: `Accounts.Add(new SurlAccount("", "tok123"))` (empty name allowed), `AllowAnonymous`, `AllowPlaintextAuthentication`, `Log` (`TextWriter.Synchronized(new StringWriter())`) with `LogLevel = LogLevel.Verbose` (`using Surl.Output;`); read the writer after `await server.DisposeAsync()`. Curl.Api `CurlAuthenticationOptions { UserName = token }` (no password) sends `auth_token`; refusal `CurlException` 67, 35 (requires TLS), 60.

## A stand-in for a service (batch 10)
Probed on Surl 5da5b3eaf / Curl c84035f797, Surl.Api 1.0.0, Curl.Api 0.2.0, 2026-11-22, windows (nats-stand-in-for-a-service and twins).
- curl `-d x "nats://h:p/subj?request"`: subscribes `_INBOX.<32 hex>` (sid 1), publishes with it as reply-to, writes the first reply (no newline added). Nobody subscribed: exit 56 `curl: (56) NATS request failed: no responders are subscribed to the subject` at once. A subscriber that never answers: waits until `-m` (28).
- curl has no responder mode. Console stand-in: a raw NATS session fed line by line from a pipe into `curl -s -T - telnet://h:p/`: `CONNECT {"verbose":false}`, `SUB prices.get 1`, `PING` (CRLF each); the `PONG` says the SUB is in; the request arrives as `MSG prices.get 1 _INBOX.<hex> <n>` then the payload; answer `PUB <inbox> <n>` and the payload. curl stays connected after stdin ends: `kill` it.
- Without `-T -` (plain stdin to telnet://), the Windows Curl port sent each CR LF as LF: control lines were accepted, but a payload not followed by CR LF got `-ERR 'Parser Error'` and a close. Reported, not claimed.
- `-v --log-file` lines `#N < PUB prices.get _INBOX.<hex> 6\r\n` (escaped) are written as the server works: poll the log with a bound.
- Curl.Api: `CurlRequest(HttpMethod.Post, "nats://h:p/prices.get?request") { Body = ... }`, the response body is the reply; no responders `CurlException` 56 `RecvError`; `CurlClientOptions.Timeout` 3 s with a silent responder 28 `OperationTimedOut`. Surl.Api has no subscribe API: a C# stand-in is a `TcpClient` NATS session. `RecordExchanges` + `server.Exchanges[].Received` (`byte[]`) holds each client's lines as they arrive.
- surl-mcp `get_exchanges`: `SUB prices.get 1\r\n` (received) once the subscriber is in; the reply to a requester is a sent chunk `MSG <inbox> 1 10\r\nprice=9.99\r\n`. `publish_message(topic: "_INBOX/<hex>", payload)` (reply subject written as a topic) answers a waiting `?request`. Agent stand-in = a concurrent curl-tool subscriber (so the request has a responder) + get_exchanges for the inbox + publish_message. Two curl-tool calls outstanding on one McpClient run side by side.

## Recipes
nats-publish-and-subscribe, nats-log-in-with-a-token (and twins), nats-stand-in-for-a-service (and twins).
