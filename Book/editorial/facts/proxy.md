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
- surl-mcp `add_proxy_account`/`remove_proxy_account` (`server_id, user, password`) -> `{"user"}`. Removing the LAST proxy account makes the proxy anonymous (no login 200); since Surl a423807a5d (BL-1479) the answer adds `"warning":"server s1 has no proxy account left and its proxy now needs no login"`. `add_account` is an origin account: the proxy still answers 407 to it.
- Events: proxy logins are `login_judged` with scheme `http`, mechanism `Digest`, account as claimed. Log: `Answered 407: GET <url>: a proxy login is needed.`, `Login accepted: Digest app-v1`, `Forwarded GET to 127.0.0.1:P; the origin answered 200.`
- The -v log holds `Proxy-Authorization: Basic <base64 user:password>` for the refused Basic login on an http proxy listener: NOT withheld in this build (reported).

## Proxy logins, tunnels and SOCKS5 (batch-08, Surl 9ad60d80c, Curl 7d167fa10a, 2026-11-21, windows)
- `-U app:pw` names the account of EVERY proxy listener (http, https, SOCKS); `--user` accounts are the origin's. Swapped: origin account at the proxy 407; proxy account at the origin passes the proxy, origin 401.
- 407 over an http proxy listener offers three `Proxy-Authenticate: Digest` (MD5, SHA-256, SHA-512-256) and NO Basic; over an https proxy listener (`--proxy https://... --cert --key`) it adds `Proxy-Authenticate: Basic realm="surl", charset="UTF-8"` and Basic is accepted (`curl --proxy-cacert server.pem -U app:pw`). Basic over http: 403 unchecked; `--allow-plaintext-auth` accepts it (warning `surl: warning: --allow-plaintext-auth: passwords and tokens are accepted over unencrypted connections`). Wrong password: 407 with the same offers.
- `--proxy-auth digest` writes `surl: warning: --proxy-auth: accepted proxy methods are digest` (a warning even when narrowing); Basic over https then 407.
- One fetch with proxy and origin logins: `login_judged` events `http Digest app` (twice: curl resends after the origin's 401) then `http Digest web`; surl-mcp `get_events` with `listen_url` separates them.
- Curl.Api: `CurlAuthenticationOptions { ProxyUserPassword, ProxyAuthentication = HttpAuthenticationMethods.Digest, UserPassword, HttpAuthentication }`; `CurlTlsOptions.Proxy` = the https proxy's `CurlTlsPeerOptions` (`CertificateAuthorityFile`), separate from `Origin` (trusting only the origin: 60 through an https proxy). Read the body before disposing the CurlClient (OperationCanceledException).
- curl-mcp `transfer`: `proxy` with `user:pw@` sends Basic (403 over http); no proxy-login, proxy-CA or `--resolve` field: `caFile` is the origin's only (https proxy: exitCode 60). Use the `curl` tool for Digest, `--proxy-cacert`, `--resolve`.
- CONNECT (any https URL through `-x`): `-v --log-file proxy.log` (no trace) holds `#N < CONNECT 127.0.0.1:P HTTP/1.1\r\n`, `#N * Tunnelling to 127.0.0.1:P; answered 200.`, `#N > HTTP/1.1 200 Connection established\r\n`, then TLS bytes as `\xNN`; `GET /hello.txt` never appears. Untrusted origin: tunnel opens, curl 60 (log `The tunnel ... failed; both connections were aborted.`). `--fault "error;scheme=http-proxy;command=CONNECT;reply=503"`: `curl: (7) CONNECT tunnel failed, response 503`, Curl.Api `CouldntConnect`, curl-mcp `transfer` failure exitCode 7 `curl exit code 7 (CouldntConnect)`. `listener=X` in a fault must be a listen URL (exit 2).
- Through the tunnel Curl.Api and the origin negotiate HTTP/2: the origin's recorded bytes start `PRI * HTTP/2.0`. get_exchanges: proxy chunks after the CONNECT head and the 200 are all base64.
- Surl.Api refuses a server whose only listeners are `--proxy` in AdditionalArguments: `ArgumentException: ListenUrls names no listen URL` (surl itself accepts a proxy-only command line). Put an origin in ListenUrls.
- SOCKS5 (positional `socks5h://127.0.0.1:P/` beside `http://localhost:O/`): log `#N * Connected to localhost:O; answered 05 00.`; socks5 (curl resolves; `--resolve origin.test:O:127.0.0.1`) `Connected to 127.0.0.1:O`; socks5h with a name only the client knows: `Answered 05 04: the origin origin.test:O could not be used (HostNotFound).`, `curl: (97) cannot complete SOCKS5 connection to origin.test. (4)`. SOCKS4 to a SOCKS5 listener: `Answered 5B: a SOCKS4 request on a SOCKS5 listener.`, `curl: (97) [SOCKS] cannot complete SOCKS4 connection to 0.0.0.0:0. (91), request rejected or failed.` Outside loopback / own listener: `05 02 ... (NotAllowed)`, `(2)`. Nothing listening: `05 05 (Refused)`.
- SOCKS5 with `-U`: no login `curl: (97) No authentication method was acceptable.` (server sent `05 FF`); right login without `--allow-plaintext-auth`: `User was rejected by the SOCKS5 server (1 1).`, log `Answered 01 01: the login is RefusedPlaintext.`; with it, 200. Curl.Api error code name for 97: `Proxy`.
- Origin on `127.0.0.1` only and socks5h to `localhost`: ~4 s on Windows (surl tries ::1 first: `Answered 05 05: the origin [::1]:O could not be used (Refused)` when curl resolves localhost itself with socks5://). Listen on `http://localhost:O/` (binds ::1 and 127.0.0.1 on Windows).
- get_exchanges of a SOCKS listener: base64 greeting `05 02 00 01`, `05 00`, request `05 01 00 03 <len> <name> <port>` (or `01` + IPv4), reply `05 00 00 04 ...`; the relayed plain HTTP then appears as text chunks. Events: only exchange_started/ended for SOCKS.
- rfc1928/rfc1929 are not in the bibliography.

## Recipes (batch-08)
http-require-a-proxy-login, https-tunnel-through-a-proxy, socks5-relay-through-a-socks-proxy (each with -in-csharp and -from-an-agent).

## Proving every request went through the proxy (http-prove-a-client-uses-its-proxy, + twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `--trace-ascii traffic.txt` traces BOTH listeners into one file; `--log-file` beside `--trace-ascii` wrote nothing. Origin heads are origin form (`0000: GET /hello.txt HTTP/1.1`) with `0056: Via: 1.1 surl` when proxied; the proxy's are absolute form (`0000: GET http://...`); the head ends with a `xxxx: ` line. The proxy writes `#N * Forwarded GET to 127.0.0.1:P; the origin answered 200.` per request (`grep -c`). A short awk fixture counts proxied/direct heads portably (no `{n}` intervals, strip `\r`).
- A direct request to the origin is served 200 with no Via: the origin cannot refuse it, so the proof has to be read from the server's record.
- Surl.Api: `RecordExchanges = true`, `server.Exchanges` filtered by `ListenUrl == BoundUrls[i]`; one exchange can hold several heads (connection reuse), so split `Received` text at `\r\n\r\n`. `CurlProxyOptions` is in namespace `Curl.Api` (file under Options/Transport). curl-mcp `transfer` answer headers include `Via` when proxied.
- surl-mcp `get_exchanges` with `listen_url`: received chunks for HTTP heads are `encoding: text`.
- Recipes (batch 12): http-prove-a-client-uses-its-proxy (and twins).
