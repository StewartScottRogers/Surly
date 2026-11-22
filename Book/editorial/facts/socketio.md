<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# socketio facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`socketio://host:port/` (TCP, `ready: port`). Served only at the HTTP path `/socket.io/`; any other path is 404. Main namespace `/` only.

## Server options that matter
- `--allow-anonymous`: a client with no login may join. Loosens (start-up warning).
- `--user alice:secret`: an account; the login travels in the namespace request.
- `--allow-plaintext-auth`: lets a password be checked over plain `socketio`. Loosens (warning `passwords and tokens are accepted over unencrypted connections`).

## Secure by default
Each of these is exit 22, stderr `Socket.IO server refused namespace /: {"message":"Not authorized"}`, nothing written:
- no login and no `--allow-anonymous`;
- the right password over plain `socketio` without `--allow-plaintext-auth`;
- a wrong password.

## Client: curl
- `-d '["event","arg"]'` is emitted after joining; surl echoes it to the sender, written one JSON array per line. `broadcast` also comes back to the sender exactly once.
- `-u user:pass` is sent in the namespace request since Curl b9ca725077 (before, it was ignored and every login was refused).
- A session has no end: every joining run ends by `-m`, exit 28, with the events already written.
- `-d` not a JSON array starting with the event name: exit 2 before connecting, stderr `the data to emit must be a JSON array whose first element is the event name`.
- Path `/admin`: exit 22, `Socket.IO server refused namespace /admin: {"message":"Invalid namespace"}`.

## MCP
Not probed.

## Gotchas
- Every case that joins needs `-m` and a case `timeout`.

## Recipes
socketio-receive-an-event

## Logging in over TLS (socketio-log-in-to-a-namespace and twins, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `socketios://` with `--cert --key --user alice:s3cret`; `curl -m 3 --cacert server.pem -u alice:s3cret -d '["chat","hi"]' socketios://...` writes the event back, exit 28. Wrong password, unknown user, no login, right password over plain `socketio://`: exit 22 `Socket.IO server refused namespace /: {"message":"Not authorized"}`. With `--allow-plaintext-auth` plain joins. No `--cacert`: 60.
- DEFECT (BL-1589, fixed in Surl master, not in this build): `--user :tok123` (token account) refuses every form tried (`-u tok123:`, `-u :tok123`, `--oauth2-bearer tok123`) over `socketios://`, `Not authorized`. The recipes' last case records the refusal; replace it when the build is fixed. Surl.Api `new SurlAccount("", "tok123")`.
- Curl.Api: `SendAsync` does not return while a session is open (cancelling a token inside it gave no body). `CurlClientOptions.Timeout` = `-m`: an accepted join throws `CurlException` 28 `OperationTimedOut`; a refusal is 22 with `Not authorized` in the message. `RecordExchanges = true` then `server.WaitForEventAsync(new SurlEventQuery { Kind = SurlEventKind.LoginJudged, AfterSequence = 0 }, ...)` cast to `Surl.Protocol.Abstractions.LoginJudgedEvent` has `IsAccepted`.
- curl-mcp `curl` tool: args `-sS -m 3 --cacert server.pem -u alice:s3cret -d [json] url`; exit 28 with the event in `standardOutput`.
