<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# proxy facts
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-20, windows.
HTTP proxy only so far (`surl --aihelp proxy`); SOCKS is its own topic (`surl --aihelp socks`).

## Listen URLs and readiness
`--proxy http://127.0.0.1:{{port}}/` (`-x`, repeatable) makes that listener a proxy; TCP, `ready: port`.
A bare `http://` listen URL beside it stays an ordinary server, so one surl can be origin and proxy:
`surl --directory site http://127.0.0.1:{{port}}/ --proxy http://127.0.0.1:{{port2}}/` (stdout: one
`Listening on` line per listener, origin first). `https://` proxy needs `--cert`/`--key` or `--self-signed`.

## Server options that matter
- `--proxy-allow-origin <addr[/prefix]>` (repeatable; loosens, "widens what a peer may do"): one address
  or range admits no warning; `*` warns at start: `surl: warning: --proxy-allow-origin: the proxy connects
  to every origin address, not only loopback`. A host name: exit 2 `expected an IPv4 or IPv6 address, an address/prefix, or *`.
- `--head-timeout <s>` bounds the origin's connect and response head: an allowed but silent origin
  (192.0.2.1 with `--proxy-allow-origin 192.0.2.1 --head-timeout 2`) is `504 Gateway Timeout` in ~2.5 s
  on Windows. A loopback port with nothing listening: `502`.
- `-U user:pw`, `--proxy-user-file`, `--proxy-auth` (default `digest,basic`): proxy accounts, 407 without.
  Not probed further yet.

## Secure by default
- Origins: loopback only. Anything else, and the proxy's own listener, is `403 Forbidden`,
  `Content-Type: text/plain; charset=us-ascii`, body `403 Forbidden\n` (14 bytes); curl exits 0.
- `CONNECT` (`curl -p`, or any https URL through `-x`) to a refused origin: `curl: (7) CONNECT tunnel failed, response 403`, exit 7.
  Curl.Api: `CurlException` `CouldntConnect` (7), same message.
- `localhost` as origin works but cost ~5 s here (tries ::1 first?): use 127.0.0.1.

## What passes through
- curl sends absolute form plus `Proxy-Connection: Keep-Alive` (Curl.Api and curl-mcp `transfer` too).
- The origin receives origin form `GET /hello.txt HTTP/1.1`, Host, User-Agent, Accept, then `Via: 1.1 surl`;
  Proxy-Connection is dropped. The answer to curl gains `Via: 1.1 surl` as its last header.
- `--trace-ascii site/origin.txt` on the origin, fetched afterwards, shows it (`0056: Via: 1.1 surl`).
- A plain GET to the proxy listener itself (origin form, no proxy): `400`.

## MCP
- `start_server`: `listen_urls` needs at least one item (JSON-RPC -32602 otherwise); put the proxy in
  `arguments: ["--proxy","http://127.0.0.1:0"]`. `bound_urls` = listen_urls then proxy. Warnings come first in `log_lines`.
- `get_exchanges`: one exchange per listener, `listen_url` tells proxy from origin; first `received` chunk is the head.
- curl-mcp `transfer` has `proxy` (as `-x`; env proxy when omitted, per its description), no tunnel field:
  use the `curl` tool with `-p` (exitCode 7, tool error, `standardError.content` holds the line).

## C#
- `SurlServerOptions` has no proxy property: `AdditionalArguments = { "--proxy", "http://127.0.0.1:0" }`;
  `BoundUrls[1]` is the proxy. `SurlExchangeRecord.ListenUrl` is a `Uri` (compare to `BoundUrls[i]`).
  `Log` + `LogLevel.Info` captures the `*` warning before the `Listening on` lines.
- Curl.Api: `CurlClientOptions.Proxy = new CurlProxyOptions { Proxy = url, HttpProxyTunnel = true }`;
  `CurlRequest.Proxy` replaces it per request. A 403 from the proxy is a normal `CurlResponse`.

## Recipes
http-run-a-test-proxy, http-run-a-test-proxy-in-csharp, http-run-a-test-proxy-from-an-agent.

## Proxy accounts (http-rotate-a-proxy-account, Surl 9ad60d80c, 2026-11-20, windows)
- `-U app-v1:old-secret` on a `--proxy` server: no login 407 with three `Proxy-Authenticate: Digest` (MD5, SHA-256, SHA-512-256); `curl -x P --proxy-digest -U u:p` 200; wrong 407. Plain `-U` (Basic) over an http proxy listener: 403, unchecked. The origin (no `--user`) serves GET with no login.
- surl-mcp `add_proxy_account`/`remove_proxy_account` (`server_id, user, password`) -> `{"user"}`. Removing the LAST proxy account makes the proxy anonymous (no login 200) with NO warning. `add_account` is an origin account: the proxy still answers 407 to it.
- Events: proxy logins are `login_judged` with scheme `http`, mechanism `Digest`, account as claimed. Log: `Answered 407: GET <url>: a proxy login is needed.`, `Login accepted: Digest app-v1`, `Forwarded GET to 127.0.0.1:P; the origin answered 200.`
- The -v log holds `Proxy-Authorization: Basic <base64 user:password>` for the refused Basic login on an http proxy listener: NOT withheld in this build (reported).
