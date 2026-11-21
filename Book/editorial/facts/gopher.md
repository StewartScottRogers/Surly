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
gopher-fetch-a-menu, gopher-fetch-a-menu-in-csharp, gopher-walk-menus-into-subfolders (and twins)

## Walking menus (gopher-walk-menus-into-subfolders and twins; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-21, windows)
- Menus are in ordinal order of name, files and folders mixed (`archive` before `today.txt`). `curl gopher://h:p/1` (type only) sends the empty selector and answers the root menu; `1/notes` and `1/notes/` answer the same menu. curl normalizes `..` in the URL path before sending.
- The item type in the URL is not sent and not checked: `1/hello.txt` answers the file, `0/notes` answers the folder menu. A missing file or folder (`1/notes/nope`, `0/notes/archive/2026.txt`) is the error menu, curl exit 0.
- Without `--list-directories`: `/`, `1/` and `1/notes` all answer the error menu; `0/hello.txt` still serves the file.
- Surl.Api `SurlServerOptions.Files["notes/archive/2025.txt"]` makes both folders. surl-mcp `start_server` takes `arguments: ["--list-directories"]` and `files`; curl-mcp `transfer` of a `gopher://` URL answers `body.content` with the menu (CR LF kept), `statusCode` 0, failure null.
- Recipes: gopher-walk-menus-into-subfolders (+ -in-csharp, -from-an-agent). A walk script needs `tr -d '\r'` before `awk -F'\t'`.
