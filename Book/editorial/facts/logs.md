<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# logs facts (surl's log, trace, events and recorded exchanges; curl's -w and config files)
Checked against: surl 1.0.0 (Surl 9ad60d80c), curl (Curl 7d167fa10a), Surl.Api 1.0.0, Curl.Api 0.2.0,
surl-mcp 1.0.0, curl-mcp 0.1.0.0, 2026-11-20, windows.

## surl's log
- Levels (`surl --aihelp logging`): none (`-s`), error (`-s -S`), info (default), verbose (`-v`), trace
  (`--trace`/`--trace-ascii <file>`). `--log-file f` appends the log to f; failures still go to stderr.
- info: startup warnings and one line per exchange, `#1 * Exchange 1 opened: https from 127.0.0.1:54278.`
  Nothing about status, path or login: not useful for diagnosis.
- verbose: every event (`#N * ...`) and every wire line, `#N < ` received and `#N > ` sent, one log line per
  protocol line, CR LF shown as `\r\n` literally (`#5 < RETR a.txt\r\n`). HTTP/2 frames show as `\xNN` escapes.
  Event lines that matter: `Login accepted: Digest app`, `Login refused: Digest app`, `Login accepted: Bearer
  bearer token` (never the token), `Login refused: ftps app`, `GET /a.txt: 200, 3 bytes of <path>`,
  `GET /a.txt: 401, a login is needed`, `GET /a.txt: 404, nothing exists at <path>` (in-memory files: a fake
  path, `C:\x5Csurl\x5CReport.csv` on Windows), Basic or Bearer over http://: `GET /a.txt: the authentication
  policy forbade the request; answered 403 and closed.` with NO login line. Paths in log lines escape `\` as `\x5C`.
- A refused login looks the same for a wrong password and an unknown account (`Login refused: Digest ap`).
- A request with no credentials: only `401, a login is needed`, no login line. A method not offered
  (`--ntlm` by default): the same.
- SECRETS (Surl ADR-0182, from Surl 9ad60d80c): -v, --trace, --trace-ascii, --log-file and Surl.Api's `Log`
  replace exactly the secret with `****`, keeping the command or scheme word: `#N < PASS ****\r\n` (FTP and
  FTPS, ACCT too), `#N < Authorization: Basic ****` / `Bearer ****` (HTTP/1.x, http and https, Proxy-Authorization
  too). HTTP/2: each HEADERS/CONTINUATION payload of a block carrying authorization is dumped as `****` after its
  9-byte frame header (trace line `0000: ..=......****`, just before `Login accepted: Basic app`). Also withheld in
  this build (by the Surl commits, not all probed): SMTP AUTH, IMAP LOGIN/AUTHENTICATE, POP3 PASS/AUTH, SASL lines
  after 334/+, SOCKS5 password, RTSP and WebSocket-upgrade Authorization, HTTP proxy. A trace's
  `Recv data, <n> bytes` counts the bytes DUMPED. No option turns it off.
- LDAP simple bind password: withheld since Surl a423807a5d (`0000: ...cn=app..****`, the user name kept; also when the bind is
  refused unchecked over ldap://: `LDAP bind refused: confidentialityRequired: simple bind needs TLS or
  --allow-plaintext-auth`). STILL LOGGED IN CLEAR per ADR-0182 (not re-probed): MQTT, Redis, HTTP/3 and the extended servers (NATS, STOMP, AMQP,
  Kafka, XMPP, MQTT 5, S3/Azure/GCS/OCI headers) until BL-1424, BL-1432..1435, BL-1458..1465 land (only LDAP
  probed). Not secrets, so never withheld: Digest/NTLM/Negotiate responses, a token in a URL query (event line
  `GET /a.txt?token=...: 401`), uploaded bodies, FTP data connections.
- The event stream keeps raw bytes by design: Surl.Api `Exchanges`/`RecordExchanges`, surl-mcp `get_exchanges`
  and the control endpoint's events hold PASS and Authorization in clear.
- With `--trace`/`--trace-ascii` given, the per-exchange log lines go into the TRACE file, not `--log-file`:
  the log file then holds only startup lines (warnings, `* Serving a throwaway certificate`). Observed, not
  documented (reported as a finding).

## Trace (`--trace-ascii`)
- Blocks `#N <= Recv data, 16 bytes (0x10)` / `#N => Send data, ...`, then data lines `0000: ...` with
  offsets; the log event lines (`#N * ...`) sit between blocks. Data lines wrap at 64 characters, so a long
  header (Digest, a long token) spans lines: grep for a secret can miss it. Holds every byte, secrets withheld as
  above. For a server still logged in clear (LDAP): awk that prints the `<= Recv data` line and skips lines until
  the next `^#[0-9]+ ` line (drop-received.sh in surl-keep-the-server-log-for-a-failed-run). Windows curl (WinLDAP)
  sends a refused simple bind twice, so count with `grep -c -m1`.

## Surl.Api
- `SurlServerOptions.Log` (TextWriter) + `LogLevel` (`using Surl.Output;`, `LogLevel.Verbose`) = `surl -v`.
- `RecordExchanges = true`: `server.Exchanges` (`SurlExchangeRecord`: `ExchangeId`, `Scheme`, `Client`,
  `Chunks`, `Received`, `Sent`); `SurlExchangeChunk.Direction` is `SurlExchangeDirection.Received|Sent`,
  `Bytes` a `ReadOnlyMemory<byte>`. Chunks are in crossing order (Received/Sent are each direction joined).
  Exchange IDs equal the `#N` of the log. A fresh `CurlClient` per request = one exchange per request;
  `server.Exchanges.Count` before the request indexes the new one.
- Over FTP(S) Curl.Api's `StatusCode` is 0 on success; a refused login throws `CurlException` `LoginDenied`.

## surl-mcp
- `start_server` refuses `--log-file`, `--trace`, `--trace-ascii`: tool error, text `refused: --log-file is not
  accepted: surl-mcp writes each server's log to its own standard error; use get_exchanges and get_events to see
  what a client sent` (from Surl 9ad60d80c; before, it named SurlServerOptions.Log). Its answer has `log_lines`
  (startup lines) only.
- `get_events(server_id, kinds:["login_judged"])`: `{events:[{sequence, at, kind, exchange_id, scheme, account,
  mechanism ("Basic", "Digest", "password" for FTP), is_accepted, is_refused_by_fault}], has_more,
  dropped_event_count}`; also `exchange_started` (listen_url, scheme, client) and `exchange_ended`
  (received_byte_count, sent_byte_count). No secrets. A login refused unchecked (plaintext) has no event.
- `get_exchanges(server_id)`: `exchanges[].chunks[] {direction sent|received, layer wire, encoding text|base64,
  data}`; FTP `PASS` in a `received` chunk in clear, still (raw by design, ADR-0182 decision 4).
- `list_servers`: `exchange_count` per server.

## curl: -w, config files, variables
- `-w "%output{>>file}..."` appends the rest of the format to file (stdout stays empty); `%{json}` is every
  variable as one JSON object (`http_code`, `size_download`, `time_total` 6 decimals, `url`, `exitcode`,
  `errormsg`, `filename_effective` "nul" on Windows with -o NUL ...). Connection refused: `http_code` 0 in
  `%{json}`, but `%{http_code}` alone prints `000` (invalid JSON number). `%{onerror}` prints the rest only
  for failed transfers.
- `-w @file`: a trailing newline in the file is dropped; write `\n` in it.
- Glob `"http://h/{a,b,c}.txt"` with one `-o NUL`: all three bodies dropped, one -w line each, one connection.
- `-K f`: options without dashes, `#` comments, `url`/`expand-url` lines; `out-null` after each URL. Options
  after `-K` on the command line override (`--no-fail-early`). `fail-early`: first failure ends the run (exit
  22 for 404); without it the exit is the last failure's (67 for a refused FTP login).
- `--variable name=value` + `expand-url = "http://127.0.0.1:{{name}}/x"`: works. An unset variable expands
  to nothing (`http://127.0.0.1:/x` -> port 80, exit 7 here).
- cmd.exe gotcha for `run:` lines: `\"` inside a double-quoted argument toggles cmd's quoting, so a `|` in a
  grep regex becomes a pipe (exit 255). Use several `-e` patterns and `.` for the quote character.
- YAML: a `run:` value holding `: ` must be quoted as a whole ('...').
- Bash tool heredocs collapse `\\` to `\`: write YAML regexes with `\\r\\n` using the Edit tool.

## curl-mcp URL tools
- `build_url(scheme, user, password, host, port, path, query, fragment, flags)`: without `UrlEncode` it pastes
  parts raw and answers a broken URL with no failure (`ftp://ann@example.com:p@ss:w/rd 100%@127.0.0.1:21/q1
  report#final.txt`); with `flags:["UrlEncode"]` it percent-encodes (`%40 %3A %2F %20 %25 %23`). Answer
  `{url, failure, isError}`.
- `parse_url(url, flags)`: `{url, parts {scheme,user,password,options,host,port,path,query,fragment,zoneId},
  failure}`; parts stay encoded unless `flags:["UrlDecode"]`. The raw URL above: tool error, failure
  `{kind UrlRefused, text "The URL parser refused url: CURLUcode 3 (MalformedInput)"}`.
- `version()`: protocols (27 on Windows) + extendedProtocols; no `rtmp`; features include `IPv6`, `SSL`.
- `start_server` takes `http://[::1]:0`; `--user` value is split at the first colon (password may hold `:`).
- `SurlServerOptions.Log` with `LogLevel.Verbose` also writes the `Listening on <url>` line first. A `TextWriter`
  adapter that forwards whole lines to a test's output works (surl-send-server-logs-to-test-output). In-memory
  404 path on Windows: `nothing exists at C:\surl\summary.csv`.

## Recipes
surl-keep-the-server-log-for-a-failed-run (+ -in-csharp, -from-an-agent), ftp-find-which-account-uploaded-a-file, ftp-see-which-command-broke-a-transfer (each + twins; FTP log lines in `ftp.md`), http-find-failed-logins-in-the-log,
http-print-the-failing-exchange, curl-check-many-urls-from-one-config, curl-write-a-test-report-with-write-out,
curl-build-test-urls-safely, curl-check-what-a-build-supports, surl-send-server-logs-to-test-output.

- Decided by Surl (2026-11-20): with `--log-file f --trace-ascii t`, exchange lines go only to t; the log file gets the dump only when there is no trace file (Surl ADR-0033 s.6). Secrets in -v/--trace/--log-file are withheld from Surl 9ad60d80c for the protocols listed above (ADR-0182); filter only what is still logged.
- Surl.Api `StopAsync` waits up to about 7 s while a client holds a connection: a 5 s shutdown grace plus a 2 s lingering close (Surl ADR-0099 decision 4). Dispose clients first.

## Surl.Api Log levels (surl-read-the-server-log-in-process, 2026-11-20)
- `Log = TextWriter.Synchronized(new StringWriter())`; read the inner writer after the server stops (exchange-ended lines are in by then). `AllowAnonymous` writes `surl: warning: --allow-anonymous: every request and login is accepted without checking credentials` first, then `Listening on`. `LogLevel.Info`: warning, Listening on, `#1 * Exchange 1 opened: ...` only. `LogLevel.Error`: nothing at all, the warning included. Verbose: as in the `surl -v` notes above; a 401 is `#1 * GET /x: 401, a login is needed` followed by three `WWW-Authenticate: Digest` lines (MD5, SHA-256, SHA-512-256).

- Not withheld after all (2026-11-20, http-rotate-a-proxy-account probe): an HTTP proxy listener's -v log shows `#N < Proxy-Authorization: Basic <base64>` for a Basic proxy login refused unchecked (403) over http. Digest Proxy-Authorization is logged too (not a secret). Reported.

- Until Surl BL-1476 lands, nothing on an http/https listener under `--proxy`, nor a SignalR hub, is redacted in `-v` and trace logs (the router hides the redactor). Keep filtering those logs, and never claim redaction for them.

## Seeing both sides of a 405 (batch 9: http-see-both-sides-of-a-request + twins, 2026-11-21, windows)
- `surl --user alice:s3cret` (no `--allow-uploads`), `curl --digest -u alice:s3cret -T f URL`: one exchange, 401 then 405 `Allow: GET, HEAD`, `Connection: close`. surl's trace/-v event lines: `#N * PUT /notes.txt: 401, a login is needed`, `#N * Login accepted: Digest alice`, `#N * PUT /notes.txt: the method is not served; answered 405 and closed.` and `Authorization: Digest ****` (Digest is withheld too). No login: `401, a login is needed` (Surl.Api log: `a login is needed, and the body announced was not read; answered 401 and closed.`). With `--allow-uploads`: 201, log `201, 9 bytes stored at <path>`. DELETE is 405 the same way.
- curl `--trace-ascii f`: `0000: PUT /notes.txt HTTP/1.1` twice, `0033: Authorization: Digest username="alice",realm=...` in clear (wrapped at 64), `0000: HTTP/1.1 405 Method Not Allowed`, `0000: Allow: GET, HEAD`. Quote `run:` values holding `: ` in YAML.
- curl-mcp `curl -v`: standardError lines end `\r\r\n` on Windows (trim CR). Its Digest header is formatted differently from curl.exe's (`username="alice", realm=...`, base64 cnonce). `get_events kinds:["login_judged"]`: accepted, refused (wrong password), none (no login).
- Curl.Api PUT with `Body` sends `Content-Type: application/x-www-form-urlencoded` (curl -T sends none).

## Timing from surl-mcp's record (http-find-a-slow-path-from-an-agent, 2026-11-21)
- `get_exchanges` chunks carry no timestamps (only `started_at` per exchange). Timing comes from `get_events` `exchange_started`/`exchange_ended` `at` (ISO 8601 with offset; `GetDateTimeOffset()` parses it). `exchange_bytes` events came back empty for HTTP here. Each curl-mcp `transfer` call is its own exchange (new connection); one `curl` tool call with several URLs is one exchange holding every request, so per-path timing needs one request per connection. `fault_applied` events name `target` and `exchange_id`. `exchange_ended` can lag the client a moment: poll until started equals ended.

- Decided by Surl (2026-11-21): `get_exchanges` caps each exchange at 64 KiB (ADR-0103 decision 3) and marks it with `is_cut`, `shown_bytes` and `recorded_bytes`. Byte events are never kept, so `exchange_bytes` is empty (ADR-0143 decision 2). There will be no Unix-socket property on SurlServerOptions (ADR-0099); use AdditionalArguments.

- Which listener (batch-10, http-tell-which-listener-was-used): the `opened:` line names scheme and client only, never the listen URL; the control endpoint (`--control`, `/v1/events?kind=exchange_started`) and Surl.Api/surl-mcp `listen_url` do. With `--log-file` and `--trace-ascii` both given the log file had no `opened:` line (as ADR-0033 s.6). Reasons for a 400 in the -v log: see http.md batch-10. surl-mcp has no log, so an agent shows a refusal through the recorded answer (or `read_file` for a 404).

- Decided by Surl (2026-11-22): the event stream (`get_exchanges`, `get_events`, control-endpoint events) keeps raw bytes, credentials included, on purpose: it is loopback-only and opt-in (Surl ADR-0182 decision 4). Only the `-v` log, trace dumps, `--log-file` and Surl.Api's Log writer redact, and no option turns that off (decision 5).

## Summarising a run from get_events (surl-summarise-a-test-run-from-events, 2026-11-22, windows)
- `store_changed` events: an upload is `store` content, `change` written, `name` the path, `byte_count`, `exchange_id`; a mail is `store` mail, `change` delivered, `name` `app@example.com/INBOX/;UID=1`. Neither carries the scheme: join `exchange_id` to `exchange_started`.
- `login_judged` comes per request with credentials: `curl --retry 1` after a 503 on one connection gives two accepted events in one exchange. curl's SMTP login to surl is `mechanism` CRAM-MD5; FTP `password`; HTTP `Digest`.
- An FTP password refused unread (no `--allow-plaintext-auth`) leaves no `login_judged` (curl 67). Sequence numbers have gaps (exchange_bytes events are not kept).

## curl variables from the environment (curl-parameterise-a-suite-with-variables, Curl c84035f797, 2026-11-22, windows)
- In a `-K` file: `variable = %NAME` imports the environment variable, `variable = %NAME=default` falls back; `expand-url`, `expand-oauth2-bearer` take `{{NAME}}` and `{{NAME:trim}}`. Not set and no default: exit 2, stderr `curl: Variable 'NAME' import fail, not set` then `curl: f.conf:3 config file option 'variable' variable expansion failure`; nothing is sent. A `--variable NAME=x` after `-K` does not change URLs the file already expanded.
- verify `run:` lines can set the environment with `env NAME=value curl ...` on all three platforms (Git's env.exe on Windows).
- surl bearer token: `--user :token`; over `http://` needs `--allow-plaintext-auth`, else 403 for the right token.

## Timed chunks, hex traces and replays (batch 13, chapter 6.9 recipes; Surl v0.1.5 d64faa4bb / Curl 2145a49eae, 2026-11-22, windows)
- surl-mcp `get_exchanges` chunks now carry `at` (ISO 8601 with offset) besides `direction`, `layer`, `encoding`, `data`: time each reply against its command from the chunks themselves (ftp-find-why-a-transfer-hangs). Exchanges carry `listen_url`, `client`, `started_at`; `get_exchanges(listen_url: ...)` filters by endpoint. `get_events` `max_events` is at most 500 (more is McpProtocolException -32602).
- The log line for a new exchange now names the listener: `#1 * Exchange 1 opened: http on http://127.0.0.1:P/ from 127.0.0.1:C.` (supersedes "never names the listen URL" above).
- `surl --trace f` (not -ascii): blocks `#N <= Recv data, n bytes (0x..)` / `#N => Send data, ...`, data lines `0000: 47 45 54 20 ... GET ` (offset, up to 16 hex bytes, text column). The hex is exact; withheld secrets are dumped as the bytes of `****` (`Authorization: Digest ****`, `Basic ****`), so `Recv data` counts the dumped bytes. An awk that turns the hex of one exchange's `<=` blocks back into bytes (LC_ALL=C, hex by `index()`, no xxd) is request-of.sh in http-replay-a-request-from-a-trace.
- `curl -s -m 10 -T request.http telnet://h:p` sends an HTTP request byte for byte (no telnet negotiation reached surl; the replay's exchange compared identical with cmp) and prints the raw response; it ends when surl closes: at once after a 400 (Connection close), else after `--idle-timeout` (`#N * Exchange N cancelled: no byte moved for the idle timeout of 2 s.`). A Digest exchange replayed: both requests 401, `Login refused: Digest` with no account name.
- `JsonSerializer.Serialize(string)` throws in a file-based app (reflection off), as mail.md says: quote strings by hand.
