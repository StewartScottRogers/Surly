<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# logs facts (surl's log, trace, events and recorded exchanges; curl's -w and config files)
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), Surl.Api 1.0.0, Curl.Api 0.2.0,
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
- SECRETS: the verbose log holds what clients sent. FTP `#N < PASS <password>\r\n` in clear (FTPS too: logged
  after TLS); HTTP/1.1 `#N < Authorization: Basic <base64>` and `Authorization: Bearer <token>` verbatim.
  Digest shows only the hash. A token in a URL query is in the event line too (`GET /a.txt?token=...: 401`).
  Over HTTP/2 the header block is HPACK bytes (not greppable, still recoverable). Nothing redacts.
- With `--trace`/`--trace-ascii` given, the per-exchange log lines go into the TRACE file, not `--log-file`:
  the log file then holds only startup lines (warnings, `* Serving a throwaway certificate`). Observed, not
  documented (reported as a finding).

## Trace (`--trace-ascii`)
- Blocks `#N <= Recv data, 16 bytes (0x10)` / `#N => Send data, ...`, then data lines `0000: ...` with
  offsets; the log event lines (`#N * ...`) sit between blocks. Data lines wrap at 64 characters, so a long
  header (Digest, a long token) spans lines: grep for a secret can miss it. Holds every byte, PASS and
  Authorization included. To keep a trace without client bytes: awk that prints the `<= Recv data` line and
  skips lines until the next `^#[0-9]+ ` line (recipe surl-keep-the-server-log-for-a-failed-run).

## Surl.Api
- `SurlServerOptions.Log` (TextWriter) + `LogLevel` (`using Surl.Output;`, `LogLevel.Verbose`) = `surl -v`.
- `RecordExchanges = true`: `server.Exchanges` (`SurlExchangeRecord`: `ExchangeId`, `Scheme`, `Client`,
  `Chunks`, `Received`, `Sent`); `SurlExchangeChunk.Direction` is `SurlExchangeDirection.Received|Sent`,
  `Bytes` a `ReadOnlyMemory<byte>`. Chunks are in crossing order (Received/Sent are each direction joined).
  Exchange IDs equal the `#N` of the log. A fresh `CurlClient` per request = one exchange per request;
  `server.Exchanges.Count` before the request indexes the new one.
- Over FTP(S) Curl.Api's `StatusCode` is 0 on success; a refused login throws `CurlException` `LoginDenied`.

## surl-mcp
- `start_server` refuses `--log-file`, `--trace`, `--trace-ascii`: tool error, text `--log-file, --trace and
  --trace-ascii belong to a host; write the log to SurlServerOptions.Log. (Parameter 'options')` (names a .NET
  property to an MCP caller: reported). Its answer has `log_lines` (startup lines) only.
- `get_events(server_id, kinds:["login_judged"])`: `{events:[{sequence, at, kind, exchange_id, scheme, account,
  mechanism ("Basic", "Digest", "password" for FTP), is_accepted, is_refused_by_fault}], has_more,
  dropped_event_count}`; also `exchange_started` (listen_url, scheme, client) and `exchange_ended`
  (received_byte_count, sent_byte_count). No secrets. A login refused unchecked (plaintext) has no event.
- `get_exchanges(server_id)`: `exchanges[].chunks[] {direction sent|received, layer wire, encoding text|base64,
  data}`; FTP `PASS` in a `received` chunk in clear.
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

## Recipes
surl-keep-the-server-log-for-a-failed-run (+ -in-csharp, -from-an-agent), http-find-failed-logins-in-the-log,
http-print-the-failing-exchange, curl-check-many-urls-from-one-config, curl-write-a-test-report-with-write-out,
curl-build-test-urls-safely, curl-check-what-a-build-supports.
