<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# telnet facts
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-20, windows; Curl.Api re-probed on Surl 9ad60d80c / Curl 7d167fa10a.

## Listen URLs and readiness
`telnet://127.0.0.1:{{port}}/`, TCP, default port 23, `ready: port`.

## Server options that matter
- `--max-line <bytes>` (default 8192, not a loosening): a longer line is answered `line too long` and the session ends, no `bye`; curl exit 0. `SurlServerOptions` has no property for it (9000 characters passes the default).
- `-v --log-file surl.log`: the log (in the cwd) holds `The client's terminal type (TERMINAL-TYPE) is vt100.`, `The client's environment (NEW-ENVIRON) is USER=ann (undefined).`, `The client's window size (NAWS) is 0x0.`; `grep` it after the fetch. Exchange 1 is verify's readiness connect, so the number after `#` varies.

## Client: curl
- The server sends the banner `surl TELNET server: each line is echoed back; quit ends the session.`, echoes each line, answers `quit` (any case; later lines unread) with `bye` and closes.
- curl reads stdin; `-T file` reads the file instead (same output, portable to run.ps1). No `quit`: curl hangs; `-m 2` gives exit 28 with the echo already written. Never run a file without quit unprobed.
- `-t TTYPE=vt100 -t NEW_ENV=USER=ann -t XDISPLOC=host:0` change nothing in the output; the server logs them.

## MCP
- curl-mcp `curl(args, stdin)` carries the session: `stdin` text, `-m 2` on a no-quit session is exit 28, failure kind `CurlExit`, tool error, `standardOutput` still holds the echo.
- surl-mcp `get_exchanges`: telnet chunks mix `encoding` base64 (option negotiation, `//oYAHZ0MTAw//A=` is IAC SB TTYPE IS vt100) and text (lines); they can share a chunk, so decode each by its encoding and search the whole.
- `start_server(arguments: ["--max-line","10"])` works.

## Curl.Api
- A request with a byte `Body` (PUT or POST; a stream not probed) to a telnet URL sends the body as `curl -T` does (Curl 7d167fa10a, BL-5594): `SendAsync` returns once the session is open and `CurlResponse.Body` streams the banner, the echoes and `bye`, then ends when the server closes. The body holds no option bytes (Curl.Api answers the negotiation). No `quit`: the body never ends; read with a timeout and dispose the response. A 9000-character line: `line too long`, then the end. Before this build the body was ignored and a line session needed a `TcpClient`.
- `CurlRequest.Telnet = new CurlTelnetOptions { TelnetOptions = ["TTYPE=vt100"] }` works and reaches the server's log (`SurlServerOptions.Log` + `LogLevel.Verbose`, `using Surl.Output;`).

## Recipes
telnet-hold-a-line-session (+ twins).
