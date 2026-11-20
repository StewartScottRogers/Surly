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

## MCP
Not probed.

## Gotchas
- A listener without `-m` never ends; never put an unbounded listener in a case.
- Two curl runs (one listening, one broadcasting) race on the handshake; claim only the caller's own copy.

## Recipes
signalr-receive-a-message
