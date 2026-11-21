<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# websocket facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-20, windows. Covers plain `ws://` file and folder serving; `wss://` is in tls.md.

## Listen URLs and readiness
`ws://127.0.0.1:{{port}}/`, TCP, `ready: port`. A plain `http://` GET to a ws listener is 426 Upgrade Required (curl exit 0).

## Server options that matter
- `--directory <dir>`: a file is sent as one message on upgrade. Two surls cannot share a directory (exit 124): a second, listings-off server needs its own folder (copy the fixtures).
- `--list-directories` (loosens, no start-up warning): a folder is accepted and answered as one text message, `hello.txt\nnotes/\n` (one name a line, folders end in `/`, no dot files, LF only), frames `\x81\x11...` then close `\x88\x00`. `/notes` and `/notes/` both work. Server log (`-v`): `Sent 17 bytes of / as a text message in 1 frames`; a file: sent as a binary message.
- Without it, a folder (and the root) is a 404 to the upgrade. Missing path and dot file are 404 too, so the 404 cannot say which.

## Client: curl
- `curl ws://.../` writes the message to stdout; the close frame writes nothing. Refused upgrade: exit 22 (with or without `-f`); `-w "%{http_code}"` gives 404 or 101. `-w %{size_download}` counts raw frame bytes (21 for a 17-byte message), do not claim it. `--trace-ascii -` shows one `Recv data, 19 bytes` frame.
- Reading `surl -v --log-file` over its own directory makes each read grow the log (TFTP read-back took 10-15 s); serve the log folder from a second surl (`--directory logs http://...{{port3}}/`).

## MCP
- curl-mcp `curl(args)` with a `ws://` URL: stdout is the message; refusal: exitCode 22, `failure.text` `curl exit code 22 (HttpReturnedError)`. `transfer(url)`: `statusCode` 101, listing in `body.content`, `failure` null, not a tool error.
- surl-mcp `get_exchanges`: chunks `received` text (the GET), `sent` text (the `101` head), then frames as `encoding: base64` (`gRFo...` = 0x81 0x11 'h'...; `iAA=` = 0x88 0x00). A refused upgrade is a `sent` text chunk `HTTP/1.1 404 Not Found`.

## Curl.Api / Surl.Api
- `SurlServerOptions { ListenUrls = { "ws://127.0.0.1:0" }, ListDirectories = true }`, `Files["notes/today.txt"]` makes a folder.
- `await using CurlWebSocket socket = await client.ConnectWebSocketAsync(url, ct)`; `ReceiveAsync(ct)` returns `CurlWebSocketMessage` (`Type` Text/Binary/Close, `Text`, `Data`, `CloseStatus`). A listing is `Text`, a file `Binary`; the server's close is the next message (`Close`, no `CloseStatus`). A refused upgrade throws `CurlException` `HttpReturnedError` (22) from `ConnectWebSocketAsync`.
- `SendAsync` with a `ws://` URL is not used here.

## Gotchas
- YAML 1.1: `args: [off]` is the boolean false; write `["off"]`.
- A bash heredoc through the tool collapses `\` and turns `\r`/`\n` inside C# or Python text into raw control characters; write C# files with the Write tool.

## Recipes
ws-receive-a-file-as-a-message, ws-list-a-folder-over-websocket (+ -in-csharp, -from-an-agent)

## Files from an agent (ws-receive-a-file-from-an-agent, Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20)
- curl-mcp `curl(["-s", "ws://.../hello.txt"])`: `standardOutput.content` is the message, `isBase64` true for non-UTF-8 (`files_base64` `AAEC/f7/` comes back as `AAEC/f7/`); `read_file` answers `text` or `base64` the same way. Missing: exitCode 22, tool error, `standardOutput` empty. `transfer` of a binary file: `statusCode` 101, `body.isBase64` true, `info.sizeDownload` counts frame bytes (10 for 6).
- `get_exchanges` first `base64` chunk of a 16-byte file: `0x82 0x10` + 16 bytes (one final binary frame).
- Recipes: ws-receive-a-file-from-an-agent.

## Large files and frames (ws-receive-a-large-file-in-frames and twins, Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-21, windows)
- surl cuts a file message into frames of 65,536 bytes: `-v` log line `Sent N bytes of /f as a binary message in K frames`: 65,535 and 65,536 -> 1, 65,537 -> 2, 131,072 -> 2, 131,073 -> 3, 200,000 -> 4. First frame bytes `0x02 0x7F` + 8-byte length (`0x10000`): opcode 2, FIN clear. A file is always a binary message.
- curl writes the reassembled message (200,000 bytes exactly, `cmp` equal with `-o`); `--trace-ascii` shows 16,384-byte reads, not frames.
- Curl.Api `ReceiveAsync` returns the whole message (`Binary`, `Data`), the next is `Close`. Surl.Api `Log` + `LogLevel = LogLevel.Verbose` (`using Surl.Output`) carries the `Sent ...` line.
- Recipe `setup:` with `head -c N /dev/urandom > site/f` needs `schema: 2`.

## Receive and close from Curl.Api (ws-receive-a-message-with-curl-api, Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-21, windows)
- A 100,000-byte file: `ReceiveAsync` gives one `Binary` message of 100000 bytes (same SHA-256); the next is `Close` with `CloseStatus` null (surl's close frame has no payload). `ReadMessagesAsync` (`await foreach`) gives the message then the Close (`Data` 0 bytes) and ends by itself; it does not send the client's close.
- `socket.CloseAsync(1000, "done", ct)` after the server's close: no exception; the server's last `Received` chunk (`RecordExchanges`) then starts `0x88` with the mask bit set. Disposing without `CloseAsync`: no close frame reaches the server. Wait for `SurlEventKind.ExchangeEnded` before reading the record.
- Missing path: `ConnectWebSocketAsync` throws `CurlException` `HttpReturnedError` (22).
