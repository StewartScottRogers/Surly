<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# http facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.
Only the limits and timeouts part so far; others add the rest.

## Listen URLs and readiness
`http://127.0.0.1:{{port}}/`, TCP, `ready: port`. `--directory <folder>` serves a folder; one surl holds a folder at a time, so give each server its own.

## Server options that matter
None of these loosens security. Each is 0 for no limit (`surl --aihelp limits`).
- `--idle-timeout <s>`: closes an exchange idle this long; default 120. Counts only silence the peer causes: while a `delay` fault holds the server the idle clock is stopped (decided in 763754785).
- `-m, --max-time <s>`: longest one exchange may take, from accept, idle or busy; default 3600. For HTTP an exchange is a connection, kept-alive requests included.
- `--head-timeout` 30 s, `--max-request-head` 100k, `--max-filesize` 100M, `--max-connections` 1024, `--max-connections-per-address` 100.
- A limit reached ends only that connection; surl keeps serving. Over HTTP/2, idle and max-time end with GOAWAY NO_ERROR.

## Secure by default
Login is judged before the method (ADR-0180): no account and no `--allow-anonymous` serves GET and HEAD, other methods 401; after a login, other methods are 405 `Allow: GET, HEAD` (`surl --aihelp http`).

## Client: curl
- `--rate 1/3s` spaces the transfers of one command line, so a kept-alive connection sits idle between them.
- `-w "%{http_code} %{num_connects}\n"`: `num_connects` 1 is a new connection, 0 a reused one; shows a server close without reading a clock.
- A request unanswered when `--max-time` runs out is closed with no bytes: curl exit 52.

## MCP
Not probed.

## Gotchas
- `--fault "delay;stage=command;ms=3000"` holds every request: with `--max-time 1` curl exits 52, with `--idle-timeout 1` it is answered 200.
- Margins of a second or more either side keep timing cases deterministic.

## Recipes
http-time-out-an-idle-client
