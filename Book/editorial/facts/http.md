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

## Served content
- Content-Type from the extension only, no charset: .html/.htm text/html, .css text/css, .js/.mjs text/javascript, .png image/png, .svg image/svg+xml, .json application/json, .jpg image/jpeg, .ico image/vnd.microsoft.icon, .woff2 font/woff2, .wasm application/wasm, .pdf application/pdf, .txt text/plain; unknown or no extension application/octet-stream.
- No index page: `/` and any folder are 404 with no Content-Type. Every 404 (and 401, and `--fault error` replies) has `Content-Length: 0`, so `--fail-with-body` has nothing to keep.
- Header order, 200: Date, Server: surl, Last-Modified, Content-Type, Content-Length. 404: Date, Server, Content-Length: 0.
- `--fault "error;scheme=http;path=/x;reply=503"` answers 503 with no `--directory` needed; quote it (`;`). Two surl processes on one `--directory`: the second exits 124.
- Ranges, always on: `bytes=100-199` 206 with `Content-Range: bytes 100-199/300` (header after Content-Type); `290-` to the end; `-20` last 20; an end past the end is cut (`280-999` -> `280-299/300`). Start past the end: 416 Range Not Satisfiable, `Content-Range: bytes */300`, Content-Length 0. Several ranges: 206 `multipart/byteranges; boundary=SURL_BYTERANGES`, each part `Content-Type`, `Content-Range`, CRLF ends, no top-level Content-Range. Invalid (`200-100`, `bytes=x`): ignored, 200 whole file.
- Any request header is accepted and served 200; `--trace-ascii` shows the head line by line (`0030: X-Api-Key: k123`), CR dropped, written before the response, so `curl -s -o NUL -H ... URL/x URL/trace.txt` (trace in the served folder) reads its own request in one command.

## Client: curl
- `-o`, `-O`, `--out-null` apply to one URL each; `--remote-name-all` gives every URL an `-O`. `-O` keeps only the last path segment; a trailing slash takes the folder name (`/docs/` -> `docs`); bare `/` -> `curl_response`. Existing files are overwritten.
- Missing folder for `-o` or `--output-dir` without `--create-dirs`: exit 23, `curl: (23) client returned ERROR on write of N bytes`.
- 404 without `-f`: exit 0 and `-o` writes an empty file. With `-f`: exit 22, no file, `-w` still prints; `-S` gives `curl: (22) The requested URL returned error: 404`. `%{url.path}`, `%{filename_effective}` (with output-dir: `dir/name`, `/` on Windows too) work.
- `--rate 1/3s` spaces the transfers of one command line, so a kept-alive connection sits idle between them.
- `-w "%{http_code} %{num_connects}\n"`: `num_connects` 1 is a new connection, 0 a reused one; shows a server close without reading a clock.
- A request unanswered when `--max-time` runs out is closed with no bytes: curl exit 52.
- Request head order: Host, User-Agent (`curl/8.22.0`), Accept `*/*`, then `-H` headers as typed (no space added after `:`). `-H` with a default's name replaces it (Host in place; User-Agent/Accept move to the end of the defaults); `-H "Accept:"` drops; `-H "X;"` sends `X:` empty; two `-H` of one name both sent.
- `-r 100-199` (both ends, from 0); 416 exits 0, with `-f` 22. `-w "%header{content-range}"` and `%{content_type}` work.
- Globbing `report-[1-12].csv` (quote it), `{a,b}` too; `-o "dir/report-#1.csv"` names from the glob. Over one connection (`num_connects` 1,0,0...). With `-f` the exit code is the last transfer's (0 if the last exists, despite earlier 404s); `--fail-early` stops at the first failure, exit 22. `-w "%{onerror}missing %{url.path}\n"` prints only for failed transfers (needs `-f` to count a 404 as failed).

## MCP
- `start_server(listen_urls*, arguments, files, files_base64)` answers structured `{server_id, listen_urls (as given, port 0 kept), bound_urls (real port, trailing /, same order), log_lines ["Listening on ..."]}`. Refusals: tool error with `{exit_code, exit_code_name, lines}` and text `surl: (N) ...` (path in a listen URL: 3 MalformedUrl); a non-loopback host, 192.0.2.1 included, is refused before binding: `{refused: "the listen host ... is not loopback; ..."}`, so 45 for an absent address cannot be shown through surl-mcp.
- `list_servers()` answers `{servers: [{server_id, listen_urls, bound_urls, started_at, exchange_count, dropped_exchange_count}]}`.
- `list_files(server_id*, path*, after_name, limit)`: `{entries: [{name, kind: folder|file, byte_count (files only)}], has_more}`, ordinal order, one level, dot files left out. `limit 2` + `after_name` pages. Errors (text only, tool error): `No folder at /nope.` (also for a file path); `docs` without `/` is `The path is not one a request may name (NotRooted).`
- `read_file(server_id*, path*)`: `{path, byte_count, modified_at, content_type, text | base64}` (text when valid UTF-8). Errors: `No file at /x.` for missing, a folder, and a dot file.
- content_type from the extension: .txt text/plain, .png image/png, .md text/markdown, .html text/html; matches the served Content-Type.
- curl-mcp `transfer(url*)` answers text JSON `{statusCode, headers [{name,value}], body {content, isBase64, totalBytes, ...}, info, failure}`; binary bodies come back base64.
- curl-mcp `curl(args*)`: `--output-dir`, `--create-dirs`, `-O` work inside CURL_MCP_ROOT. `--output-dir ..` or `-o ../x` is `PathRefused` (exitCode, streams null, tool error), text `Refused <opt>: <opt> '<given>' resolves to '<abs>', which is outside CURL_MCP_ROOT ('<root>').` Quirk: `-o` is resolved against the root, ignoring `--output-dir`, so `--output-dir downloads -o ../x` (inside for real curl) is refused. `-f` on 404: exitCode 22, failure kind CurlExit, tool error; standardOutput still holds `-w`.
- `start_server` accepts `files: {"empty.txt": ""}` (served 200, empty body); `transfer` of it and of a 404 both give body content "" and failure null.
- `transfer` `headers: [{name, value}]` are sent after Host and Accept, in order, duplicates kept; `userAgent` goes after Host. An empty value sends `Name:`; an empty `Accept` replaces the default with `Accept:` at the end (cannot drop it). No range field: put `Range` in headers; 416 is failure null, not a tool error. `transfer` does not glob (brackets sent, 404). From C#, pass `headers` as `JsonArray`/`JsonObject` (cast to `JsonNode` on `Add`): a `List<Dictionary<..>>` throws NotSupportedException in the SDK's serializer.
- `curl` tool refuses `-o` holding `#1`: PathRefused, `Refused -o: -o '<x>' names a file with a #<n> glob reference, which curl fills from the URL, so it cannot be checked before curl runs.` Use `-O --output-dir`. Under verify, `standardOutput` came back with CRLF line ends on Windows; trim lines.
- `get_exchanges`: the first chunk with `direction` `received` holds the whole request head as text (`data`), CRLF kept.

## Surl.Api (in-process, C#)
- `SurlServer.BoundUrls` is `IReadOnlyList<Uri>`, `BoundUrl` a `Uri` (the first); `ListenUrls` stays as given.
- `SurlServerStartException.ExitCode` prints as its name (`BindFailed`); `Message` is `(45) Could not bind ...: <platform reason>` (Windows: `Address not available`); path in a listen URL: `(3) URL rejected: A listen URL cannot have a path, query or fragment`.
- `DataDirectory` keeps `<dir>/.surl/lock` while running; `/.surl/...` is 404. Second server on the same folder: 124 `DataDirectoryInUse`. `DataDirectory` + `Files`: `ArgumentException: DataDirectory and Files cannot both be given. (Parameter 'options')`.
- `Files` key errors (ArgumentException): `Files holds '<key>', which is not a '/' separated relative path with bytes.` for `/x`, `../x`, `a\b`, `a//b`, `c:x`; `Files holds 'docs' and a file inside it.` for a folder key. A dot-name key is accepted and 404.
- `StopAsync` returns `SurlExitCode` (namespace `Surl.Protocol.Abstractions`) `Ok`; again returns the same. With a `CurlClient` still holding a kept-alive connection it takes about 7 s; with clients disposed, a few ms. After stop: `CurlException` 7 `CouldntConnect`.
- Curl.Api `CurlRequest.Headers` and `RemovedHeaders` are init-only `IReadOnlyList`s (build the list first); Headers are sent as given, duplicates included, empty value as `Name:`; `RemovedHeaders = ["Accept"]` drops the default Accept. Curl.Api sends Host and Accept only. No client-wide default headers, no range option (use a `Range` header). One client over a series: one connection, one recorded exchange.
- Curl.Api `CurlResponse`: `Version` is `System.Version` (prints `1.1`), `Headers` is `IReadOnlyList<KeyValuePair<string,string>>` in received order and casing; `Trailers`, `Info` too. A 404 never throws from `SendAsync`.

## Gotchas
- Port 0: `surl` writes `Listening on <scheme>://<host>:<port>/` to stdout, one per listen URL; `-s` (and `--log-level none`) suppresses it, so a script waiting for it must not pass `-s`. Exchange log lines go to stderr. A console recipe that reads the port runs its own script (`run: sh find-port.sh`, the script as a fixture); `sh` is found on Windows through Git.
- `run:` lines run under `cmd.exe` on Windows; only a leading `surl`/`curl` is replaced by the pinned binary, so a script finds them on PATH.
- `--fault "delay;stage=command;ms=3000"` holds every request: with `--max-time 1` curl exits 52, with `--idle-timeout 1` it is answered 200.
- Margins of a second or more either side keep timing cases deterministic.

## Seeding, resetting, snapshots
- `--allow-anonymous --allow-uploads`: anonymous HTTP PUT 201 (new) / 204 (over); FTP `curl -T` works. `--allow-anonymous` alone: PUT 405 `Allow: GET, HEAD`, FTP upload exit 25. Neither, no account: PUT 401, FTP anonymous login exit 67. Only `--allow-anonymous` warns at start. HTTP DELETE with both on: 405.
- A served `--directory` gets `<dir>/.surl/lock` (`ls -A` shows `.surl`). PUT to `/.surl/x` (logged in, uploads on): 409 Conflict.
- `setup:` lines run under cmd.exe on Windows; Git's `rm`, `cp`, `ls`, `grep` are on PATH there, and `&&` works.
- A script may start and `kill` its own surl, then `wait`; the lock is released and the next `rm -rf` of the folder works on Windows.
- surl-mcp: `reset_server` answers `{server_id}`; `snapshot_server` `{snapshot_id: p1}` (p1, p2 ... per host); `restore_server` `{server_id, snapshot_id}`. Newest 4 kept: older or another server's id is a tool error `server s1 has no snapshot p1; only its newest 4 snapshots are kept`. On `--directory`: tool error `Content: the store could not be snapshotted.` (or `reset.`) `The content store is on disk (--directory): reset, snapshot and restore are refused so a test never empties a real directory.` `get_events(kinds:[store_changed], store: content)`: `change` `seeded` (put_file), `written` (upload), `deleted`, `restored` (name `/`).
- curl-mcp `curl -T` into a missing FTP folder: exit 9. `curl -Q "DELE x" ftp://h/` deletes but then exits 19 (the listing of `/` is refused without `--list-directories`).
- Surl.Api seeds: `SurlSeedRefusedException` (an `InvalidOperationException`), `Store` (`StoreKind` `Mail`, `Content`), `Refusal`: `NoSuchAccount`, `InvalidAddress` (mail), `NotPermitted` (`/.surl/...`), `TooLarge` (`MaxFileSize`, which also caps mail: `MessageTooLarge`). `Message`: `Mail: Mail for bob@example.com was refused: NoSuchAccount.` A relative path is `ArgumentException ... (NotRooted)`. A refused seed publishes no event; a taken mail seed is `StoreChanged Mail Seeded ann@example.com/INBOX/;UID=1`. `Mailboxes` needs `MailDomains`.
- Curl.Api cannot upload over FTP: `HttpMethod.Put` with a bytes or stream body to `ftp://` sends RETR (downloads), status 0, file unchanged. Upload over HTTP instead.

## Recipes
http-time-out-an-idle-client
surl-find-the-port-it-chose, surl-find-the-port-it-chose-in-csharp, surl-find-the-port-it-chose-from-an-agent
http-serve-a-folder-from-disk-in-process, http-serve-files-from-memory, http-stop-a-server-and-read-its-exit-code
http-list-a-servers-files-from-an-agent, http-read-a-file-back-from-an-agent
http-download-to-a-chosen-folder (+ -in-csharp, -from-an-agent), http-fail-a-script-on-a-missing-file (+ twins), http-serve-a-small-website (+ twins), http-show-the-response-headers-in-csharp
http-check-headers-reach-the-server (+ twins), http-download-a-byte-range (+ twins), http-download-a-numbered-series (+ twins)
http-seed-a-site-from-fixture-files (+ twins), http-snapshot-before-a-risky-step, surl-catch-a-seed-the-server-refuses, surl-reset-by-copying-a-clean-directory
