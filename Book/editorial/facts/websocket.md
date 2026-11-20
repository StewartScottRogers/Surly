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
