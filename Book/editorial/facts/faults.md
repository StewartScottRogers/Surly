<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# faults facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), surl-mcp 1.0.0, curl-mcp 0.1.0.0, Surl.Api 1.0.0, Curl.Api 0.2.0, 2026-11-19, windows.
surl's fault injection (`--fault`) and the HTTP request-head limit, and what curl sees. Limits and timeouts in general: `http.md`.

## Listen URLs and readiness
Any listen URL; HTTP here, `ready: port`. An `accept`-stage rule with `times` is spent by verify's readiness connection (it opens and closes with no byte), so `times` rules in console recipes use `stage=command`; or add the rule after start (`add_fault`, `SurlServer.Faults.Add`).

## Server options that matter
- `--fault <kind>[;<key>=<value>]...`, repeatable, at most 256; loosens ("yes, for tests only"), writes `surl: warning: --fault: N fault rule(s) make this server misbehave on purpose` on start. Quote it (`;`). Kinds: `delay` (`ms=0..600000`, at accept or `stage=command`), `drop` (at accept, `stage=command`, or `after-bytes=<n>`), `refuse-login`, `error` (`reply=` in the scheme's error shape; needs `scheme=` or `listener=`), `out-of-space`. Scope keys: `listener`, `scheme`, `account`, `path`, `command`; `times=1..1000000`. Rules tried in order, first match wins. `surl --aihelp testing` lists it all.
- `path=` matches by whole segments: `path=/slow` holds `/slow/report.txt`, not `/slowness.txt`. `path` needs `stage=command` for delay/drop (at accept there is no path): exit 2, `key path cannot be given at stage accept`.
- A `delay` stops the `--idle-timeout` clock while it waits; `--max-time` still counts it.
- `--max-request-head <bytes>` (http, rtsp, s3, oci, websocket): default 100k, suffixes k m g t p (1024 each), 0 no limit, loosens nothing. `1x` -> exit 2, `surl: option --max-request-head: is badly used here`.

## Secure by default
Over the head limit: `HTTP/1.1 431 Request Header Fields Too Large`, headers Date, Server, `Content-Length: 0`, `Connection: close`; returned even for a 200,000-byte header mid-send (no reset seen). curl exits 0, `-f` 22. The default log level logs nothing about it (only `Exchange N opened`).

## Client: curl
- `drop;stage=command`: curl exit 56 `Recv failure: Connection was reset` (Windows; surl aborts, so RST). `--retry 1` alone does NOT retry it (exit 56, `%{num_retries}` 0); `--retry 1 --retry-all-errors` does (exit 0, `num_retries` 1). With `-sS`, the curl port prints the first attempt's `curl: (56) ...` line even when the retry succeeds.
- `delay` + `-m 1`: exit 28, `Operation timed out after 1006 milliseconds with 0 bytes received`. No limit: 200 after the hold; `%{time_total}` shows it.
- `-Z` with two URLs on one host over http: WITHOUT `--parallel-immediate`, the second transfer waits for the first connection (PIPEWAIT), so a fast path behind a delayed one also times out (both `000 28`). With `--parallel-immediate` the fast one is 200 at once. Whole command exits 28 if any transfer did; `-w "%{url.path} %{http_code} %{exitcode}\n"` gives a line per transfer in completion order.
- `-H @file` reads header lines from a file: keeps big cookies off the command line.
- `error;scheme=http;reply=503;path=/x;times=2` (`reply=503` alone is enough; reason phrase `Service Unavailable`, `Content-Length: 0`, no Retry-After; connection kept alive, `-f` closes it). `curl --retry 3`: exit 0, `num_retries` 2; with `--no-progress-meter` stderr is `Warning: Problem : HTTP error. Retrying in 1 second. 3 retries left.` then `... Retrying in 2 seconds. 2 retries left.` (doubling back-off; upstream wording may be `Will retry in`). `--retry-delay 1`: 1 s each. `-s` hides the warnings. `-f --retry 1`: exit 22, `-w` still prints `503 1`. A 404 is not retried (`404 0`).
- Timeout is transient: `-m 2 --retry 1` on a `delay;times=1` -> `200 1`, stderr `curl: (28) ...` then `Warning: Problem : timeout. Retrying in 1 second. 1 retry left.`; `-m` is per try (every request held: exit 28, `000 1`). `--connect-timeout 2` does not stop a `stage=command` delay (200 after the hold).
- Refused connection (nothing listening, Windows: immediate, `curl: (7) Failed to connect to 127.0.0.1:<port> after N ms: Could not connect to server`): `--retry 5` alone makes no retry (`0`); `--retry-connrefused` does, notice `Warning: Problem : connection refused. Retrying in 1 second. 5 retries left.`
- Server-side count on the console: `surl -v --log-file surl.log ...` then `grep -c` in a case (runs under cmd.exe through Git's grep). Lines: `#N < GET /x HTTP/1.1\r\n` (escaped), `GET /x: answered 503 Service Unavailable by fault rule f1.`, `GET held for 5 s by fault rule f1.` (logged for a request whose client gave up too), `surl: warning: --fault: 1 fault rule(s) ...`. `--log-level` takes none, error, info, verbose, trace (not debug).

- SMTP: `error;scheme=smtp;command=RCPT;reply=451 4.3.0 try later;times=1` (details and curl's side in `mail.md`).

## MCP
- `start_server(arguments: ["--fault", "..."])` or `["--max-request-head", "1k"]`: options as surl's words.
- `add_fault(server_id, fault)` -> `{fault_id: "f1"}`; refusal is a tool error with surl's text. `list_faults` -> `{faults: [{fault_id, item, remaining}]}` (remaining null = no limit). `remove_fault(server_id, fault_id)`.
- `get_events(kinds: ["fault_applied"])` -> events with `rule_id`, `fault_kind`, `stage`, `target` (the path). One per time a rule acted.
- `list_servers` -> per server `exchange_count` (connections for HTTP); counts the dropped one.
- `get_exchanges`: chunks with `direction` (`received`/`sent`) and `data`; join `sent` data to see `HTTP/1.1 431 ...`.
- curl-mcp `transfer`: one attempt per call, no retry. Drop -> tool error, `failure.exitCode` 56. `timeoutSeconds` -> failure exitCode 28, tool error. `headers` must be a `JsonArray` of `JsonObject {name, value}` (a `Dictionary<string,string>[]` or anonymous type fails: no JSON type info in the SDK's serializer). Two `transfer` calls at once on one McpClient run side by side.
- curl-mcp `curl(args)` runs real curl options: `--retry 1 --retry-all-errors -w %{num_retries}` works; answer `{exitCode, standardOutput{content}, standardError{content}}`. A nonzero exitCode (22 from `-f`) is a tool error. stderr carries the retry notices.
- `transfer` of a 503: statusCode 503, failure null, not a tool error. Timeout failure `kind` is `CurlExit`. Refused connection: failure exitCode 7, tool error.
- A `curl` tool call and a `start_server` on the other McpClient run at once (`CallToolAsync(...).AsTask()`, then start the server): shows `--retry-connrefused` waiting for a server.
- `start_server` refusals carry `exit_code`/`exit_code_name` in structured content: 45 BindFailed (`surl: (45) Could not bind http://127.0.0.1:P/: Address already in use`, port held by a plain TcpListener too), 3 MalformedUrl for port 70000 (`URL rejected: Port number was not a decimal number between 0 and 65535`).

## Surl.Api (in-process, C#)
- `SurlFault.DropConnection()`, `.Delay(TimeSpan)`, `.RefuseLogin()`, `.ProtocolError(reply)`, `.OutOfSpace()`; narrow with `with { Stage = FaultStage.Command, Path = "/x", Times = 1 }`. `FaultStage` (namespace `Surl.Protocol.Abstractions`): Accept, Login, Command, StoreWrite.
- `SurlFault.ProtocolError("451 4.3.0 try later") with { Scheme = "smtp", Command = "RCPT", Times = 1 }` works; `Faults.List()[0]
  .Remaining` is 0 once spent, null with no `Times`, unchanged if never reached.
- `SurlServerOptions.Faults.Add(rule)` at start (ids f1, f2...); `server.Faults.Add/Remove/List/Clear` while serving; `List()` gives `SurlFaultRule {Id, Item, Remaining}`.
- No `SurlServerOptions` property for `--max-request-head`: use `AdditionalArguments.Add("--max-request-head"); ...Add("1k")`.
- `RecordExchanges = true` then `server.Exchanges.Count` (2 after a drop and a retry; no readiness connection in-process) and `server.FindEvents(new SurlEventQuery { Kind = SurlEventKind.FaultApplied }).Count`.

## Curl.Api
- Dropped connection: `SendAsync` throws `CurlException` (`Curl.Api.Errors`), `Code` is `Curl.Api.Codes.CurlCode` `RecvError` (56). Timeout: `OperationTimedOut` (28). No retry option seen; retry in the program.
- `CurlClientOptions.Timeout` is a `TimeSpan` (not nullable): leave it unset for no limit. `CurlClient.Create(options)`.
- Two `SendAsync` at once on one client run side by side (no PIPEWAIT hold-up).
- `CurlRequest { Headers = [new("Cookie", "...")] }`. A 431 is a `CurlResponse`, no exception.
- First transfer of a cold program can take 1 to 2 s more (engine start); time claims need margins of seconds, measured per transfer.
- A 503 from `SurlFault.ProtocolError("503") with { Scheme = "http" }` is a `CurlResponse` (StatusCode 503, ReasonPhrase `Service Unavailable`); retry in the program. `ConnectTimeout` does not stop a command-stage delay. A `CancellationToken` cancelled mid-transfer throws `OperationCanceledException` (exact type), not `CurlException`. Nothing listening: `CurlException` `CouldntConnect` (7), at once on Windows.
- `SurlServerStartException` for a taken port (a `TcpListener` holding it): `ExitCode` `BindFailed`, `Message` `(45) Could not bind http://127.0.0.1:P/: Address already in use`; port 70000: `MalformedUrl` (3). Catch with `when (e.ExitCode == SurlExitCode.BindFailed)` and restart on port 0.

## Gotchas
- In Python heredocs through the Bash tool, `\\n` in a string came out as a real newline in the file: write YAML with `\n` escapes through the Write/Edit tools.
- RFC 6585 (which defines 431) is not in `references/bibliography.adoc`.
- A script that backgrounds surl and `kill`s it almost at once (Git Bash, Windows) can leave surl running: kill only after surl is known to be up. `surl --log-level debug` is exit 2.
- A YAML `claim:` or `run:` with `: ` in it needs quoting (`'grep -c "x: y" surl.log'`).

## Recipes
http-refuse-an-oversized-request-head (+ -in-csharp, -from-an-agent)
http-recover-from-a-dropped-connection (+ -in-csharp, -from-an-agent)
http-slow-down-one-path (+ -in-csharp, -from-an-agent)
http-lock-out-one-account (+ -in-csharp, -from-an-agent): `refuse-login;account=`, see `http-auth.md`
http-retry-a-flaky-endpoint, http-time-out-a-hanging-server, http-retry-until-the-server-is-up, surl-recover-when-a-port-is-taken (each + -in-csharp, -from-an-agent)
smtp-survive-a-temporary-rcpt-failure (+ -in-csharp, -from-an-agent)
http-stop-a-client-hammering-a-refused-login (+ -in-csharp, -from-an-agent): refuse-login and a client's retries, see `http-auth.md`
ftp-run-out-of-space-on-upload (+ -in-csharp, -from-an-agent), ftp-resume-a-dropped-download (+ twins): out-of-space and drop on FTP, see `ftp.md`

## Busy for exactly N requests (http-retry-through-a-busy-server and twins, Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20)
- `error;scheme=http;reply=503;path=/report.txt;times=2` + `curl --retry 2 --retry-delay 1 -w "%{http_code} %{num_retries}"`: `200 2`, stderr `Retrying in 1 second. 2 retries left.` then `... 1 retry left.`; `%{time_total}` is the last attempt only, so time the whole command in a script (`date +%s`).
- `--retry 5 --retry-delay 3 --retry-max-time 1 -f`: the port still made one retry (`503 1`, exit 22); upstream would likely make none. Not claimed.
- Surl.Api `Log = new StringWriter()` with `LogLevel.Verbose` holds ` < GET /x HTTP/1.1` per request: count them per path. surl-mcp `get_exchanges` `received` chunks hold each request line (one HTTP connection may carry several); `get_events fault_applied` count and `list_faults` remaining agree.
- surl-mcp and curl-mcp are on PATH only under verify: a program that starts them cannot be run by hand.

## out-of-space and after-bytes on FTP (ftp-run-out-of-space-on-upload and twins, ftp-resume-a-dropped-download, 2026-11-20)
- `out-of-space` takes only scope keys and `times` (`stage`, `command`, `after-bytes` refused, exit 2). FTP: `552` to STOR/APPE before any data; HTTP PUT: `413`. Event `fault_applied` with `fault_kind` `out_of_space`, `target` the path. Details and the log's wording in `ftp.md`.
- `drop;after-bytes=N` on FTP counts control-connection bytes only, so it cannot cut a download mid-file; `path=` with `after-bytes` needs a stage it cannot have (`after-bytes cannot be given with stage=command`). Use `drop;stage=command;command=RETR;path=/x;times=1` and make the partial file in setup.
- Git Bash rewrites `path=/x` in a `--fault` argument to a Windows path (MSYS path conversion), so the rule silently never matches: `MSYS_NO_PATHCONV=1` when probing by hand. verify runs cases under cmd, which does not.

- Decided by Surl (2026-11-20): an `out-of-space` fault answers HTTP 413, reusing the "too large" answer (Surl ADR-0129 decision 3), not 507. A cancelled request under a `delay` fault keeps its connection slot until the delay ends (BL-1520/1521 open).
