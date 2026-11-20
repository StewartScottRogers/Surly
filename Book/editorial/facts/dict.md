<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# dict facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`dict://127.0.0.1:{{port}}/`, TCP, default port 2628, `ready: port`.

## Server options that matter
`--directory <dir>`: each file directly in the root is a headword (name = word, bytes = definition). One database, `surl`, "Files served by surl". Nothing here loosens security.

## Secure by default
Dot files and directories are never headwords (552 no match).

## Client: curl
`curl -s dict://.../d:<word>` (DEFINE), `m:<word>` (MATCH, prefix by default), `m:<word>:*:exact`, `show:db`. Every DICT reply, 552 and 550 included, is curl exit 0. Replies end lines in CR LF.

## MCP
- `start_server` with `listen_urls: ["dict://127.0.0.1:0"]` and `files: {word: definition}`.
- curl-mcp `curl(args*, stdin)`: answers text JSON `{exitCode, standardOutput {content, isBase64, totalBytes, ...}, standardError {...}, failure, isError}`; content keeps the server's CR LF.
- curl-mcp `transfer` also reaches `dict://`: `statusCode` 0, `headers` [], the reply in `body.content`.

## Gotchas
The greeting's message ID counts up per connection (`<1@surl>`, `<2@surl>`): match it with `\d+`.

## Recipes
dict-look-up-a-word, dict-look-up-a-word-from-an-agent, dict-match-words-by-prefix (and twins)

- One surl can serve DICT beside HTTP, FTP, TFTP, Gopher and WebSocket from one --directory; a file at the top is a headword (`d:hello.txt`); the definition goes out with CR LF line ends. See http.md "One folder, six schemes" (http-serve-one-folder-every-way, 2026-11-20).
