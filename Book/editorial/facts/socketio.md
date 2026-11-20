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
