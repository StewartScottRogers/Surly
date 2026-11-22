<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# signalr facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`signalr://host:port/` (TCP, `ready: port`); `signalrs://` is the same hub inside TLS. The hub is served at `/`.

## Server options that matter
- `--allow-anonymous`: a client with no credentials may negotiate. Loosens; start-up warning `--allow-anonymous: every request and login is accepted without checking credentials`.
- `--user name:password`: an account; the secure form is `signalrs` with one.

## Secure by default
No account and no `--allow-anonymous`: the negotiate request is refused 401; curl exit 22, stderr `The negotiate request failed with HTTP status 401.`

## Client: curl
- `-d '{"type":1,"target":"Broadcast","arguments":["x"]}'` is the one message sent after the handshake. Type 1 is an invocation, type 4 a stream invocation.
- Hub methods: `Broadcast` calls the caller back as `Broadcast` (curl writes `["Broadcast","x"]`); `Echo` returns its argument, written alone (`"hi"`); `Count` n as a stream writes 1..n one per line. An invocation ends the run at its completion, exit 0.
- Unknown method: exit 22, stderr `SignalR hub method failed: Unknown hub method.`
- No `-d`: curl only listens. Since Curl b9ca725077 it keeps the connection alive, so the hub's 30-second server timeout no longer closes it (before, exit 22 at about 30 s with `Server timeout elapsed without receiving a message from the client.`). A listener runs until `-m`, exit 28.

## Streams (probed 2026-11-20)
- `-d '{"type":4,"target":"Count","arguments":[5]}'` writes 1 to 5, one per line, exit 0; `[0]` writes nothing, exit 0.
- Refusals, all exit 22, stderr `curl: (22) SignalR hub method failed: <text>`: unknown method `Unknown hub method.`;
  type 1 on `Count` or type 4 on `Echo`: `The method was invoked with the wrong message type.`; `Count` with `-1` or
  `"x"`: `Invalid arguments for the hub method.`
- curl-mcp wording for the 401: `curl: (22) SignalR: The negotiate request failed with HTTP status 401.`

## MCP
- surl-mcp `start_server` with `arguments: ["--allow-anonymous"]`; `log_lines[0]` is the loosening warning.
- curl-mcp `curl` args `-s -S -d <json> <signalr url>`; a hub error is a tool error (`isError`, `exitCode` 22,
  `failure.kind` CurlExit); stderr ends in CRLF.
- surl-mcp `get_events` with `kinds:["exchange_started"]`: two exchanges per call (negotiate, then the WebSocket).

## C#
- Surl.Api `SurlServerOptions.AllowAnonymous = true` is `--allow-anonymous`; listen `signalr://127.0.0.1:0`.
- **Curl.Api 0.2.0 cannot send the call**: `SendAsync` to a `signalr://` URL with `Body` (POST, PUT, FromStream) connects,
  negotiates and does the handshake, then sends no call message and waits (timeout 28, 0 bytes; surl logs the client
  closing without a WebSocket Close). So the C# recipe speaks the hub protocol by hand: POST `/negotiate?negotiateVersion=1`
  (JSON `connectionToken`), `ClientWebSocket` to `ws://host:port/?id=<token>`, handshake `{"protocol":"json","version":1}`
  plus U+001E, answer `{}`; a stream call is `{"type":4,"invocationId":"1","target":"Count","arguments":[5]}`; replies are
  type 2 (`item`), type 3 completion (`error` when refused), type 6 ping.

## Gotchas
- A listener without `-m` never ends; never put an unbounded listener in a case.
- Two curl runs (one listening, one broadcasting) race on the handshake; claim only the caller's own copy.

## Recipes
signalr-receive-a-message, signalr-stream-items-from-a-hub (and twins)

## Logging in over TLS (signalrs-log-in-to-a-hub and twins, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `signalrs://` with `--cert --key --user alice:s3cret`; `curl --cacert server.pem -u alice:s3cret -d <Echo json> signalrs://h:p/` writes `"hi"`. Wrong password, unknown user, no login: negotiate 401, exit 22. Plain `signalr://` with ANY password (right or wrong): 403; no login on plain: 401; with `--allow-plaintext-auth` the right password works (wrong: 401). No `--cacert`: exit 60.
- Curl.Api: the negotiate step alone works as `HttpMethod.Post` to `https://h:p/negotiate?negotiateVersion=1` (replace `signalrs://` by `https://`, `signalr://` by `http://`) with `CurlAuthenticationOptions` and `CertificateAuthorityFile`: 200 with `connectionToken`, 401/403 are responses (`StatusCode` int), untrusted is `CurlException` 60. surl-mcp `start_server` and curl-mcp `curl` tool as for the console.
