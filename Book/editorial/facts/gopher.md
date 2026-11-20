<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# gopher facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-20, windows.

## Listen URLs and readiness
`gopher://127.0.0.1:{{port}}/`, TCP, default port 70, `ready: port`.

## Server options that matter
- `--directory <dir>`, `--list-directories` (loosens, no warning). With it, the empty selector and a folder answer a menu; without it both answer the error menu, while `0/hello.txt` still serves the file.
- Menu: lines `<type><display>\t<selector>\t<host>\t<port>` ended CR LF, then `.` CR LF. File type `0`, folder `1`; sub-menu entries carry full selectors (`/notes/today.txt`); host `127.0.0.1`, port the listener's.
- Miss, dot file, listings off: the error menu `3Nothing is served at this selector.\t\terror.host\t1`, no echo of the selector.

## Client: curl
`curl gopher://host:port/0/hello.txt` sends selector `/hello.txt` (the character after the first slash is the item type and is not sent); `1/notes` sends `/notes`; the root sends an empty line. Every reply is exit 0. Replies use CR LF (the verify harness hides it for console `stdout-matches`).

## Curl.Api / Surl.Api
`ListenUrls = { "gopher://127.0.0.1:0" }`, `ListDirectories`, `Files["notes/today.txt"]`; `SendAsync(new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}1/notes"))` returns the menu as the body, `StatusCode` 0, nothing thrown on a miss. `server.BoundUrl` is a `Uri` (`.Port`). With `RecordExchanges = true`, `server.Exchanges[^1].Received` is the selector line. Split a menu on `"\r\n"`, not `'\n'`.

## Recipes
gopher-fetch-a-menu, gopher-fetch-a-menu-in-csharp
