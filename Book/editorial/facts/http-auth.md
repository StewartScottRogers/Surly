<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# http-auth facts (Digest, NTLM, Negotiate, Bearer, Basic)
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19, windows; Linux and macOS from CI output of the same builds.

## Listen URLs and readiness
`http` 80, `https` 443 (needs `--cert` or `--self-signed`); TCP, `ready: port`. One `--directory` per
surl: refusal servers run with none.

## Server options that matter
- `-u` / `--user alice:secret`: once an account exists, every request needs a login.
- `--auth <methods>` (loosens, warns naming the methods): sets what is accepted and offered. HTTP
  words: negotiate, ntlm, digest, basic, bearer, aws-sigv4. `--auth ntlm` offers `WWW-Authenticate:
  NTLM` alone and turns the defaults off. `--auth negotiate` offers `Negotiate` (NTLM inside, or
  Kerberos with `--keytab`). NTLM and Negotiate are bound to the connection; not over HTTP/2.
- `--allow-plaintext-auth` (loosens, warns): Basic and Bearer over `http://` are checked and offered.

## Secure by default
- Default 401 over `http://`: Digest only, three headers in order `algorithm=MD5`, `SHA-256`,
  `SHA-512-256`, same nonce, `realm="surl", qop="auth"`. No NTLM, no Negotiate.
- Basic or Bearer over `http://`: 403 Forbidden, unchecked, right password included.
- `curl --ntlm` against a default server: 401.
- No account and no `--allow-anonymous`: GET/HEAD served, other methods 401 (ADR-0180).

## Client: curl
- `--digest -u`: answers the first challenge (MD5); no option picks another. `--anyauth` picks Digest.
- `--ntlm -u`: logs in over `http://`. Wrong password: 401; `-f` exit 22, stderr has `401`.
- `--negotiate -u`: with no Kerberos, cannot log in anywhere: 401 stands, `-f` exit 22, no stdout.
  From 8.22.0 Negotiate does not fall back to NTLM (upstream commit a8881e5e1d).
- `-s -o {{null}} -w "%{http_code}"` prints the status alone; `-s -i` or `-D -` for headers.

## MCP
curl-mcp `transfer(url, method, headers, ...)` or `curl(args)`. No surl-mcp tool specific to HTTP logins.

## Gotchas
- `curl -v` Digest `Authorization` layout is the platform's: Windows (SSPI) writes
  `username="alice",realm="surl",...,algorithm=MD5,response="...",qop="auth"`; Linux and macOS write
  `username="alice", realm="surl", ..., qop=auth, response="...", algorithm=MD5`. Check fields with
  lookaheads, `(?=[^\r\n]*\balgorithm=MD5\b)`, not the layout.
- `--negotiate` error wording: Windows `SEC_E_NO_CREDENTIALS` (SSPI); Linux `gss_init_sec_context()
  failed: No credentials were supplied...`; macOS `gss_init_sec_context() failed: Unspecified GSS
  failure`. Wording cases are `platforms: [windows]`.
- On Linux and macOS curl prints `Ignoring duplicate digest auth header.` for the 2nd and 3rd challenge.

## Basic only over https
- `--auth basic` with `--cert/--key` and `https://` + `http://` listeners in one surl: https 401 offers `WWW-Authenticate: Basic realm="surl", charset="UTF-8"` alone (use `curl --http1.1`; HTTP/2 lower-cases names), right password 200, wrong 401, `--digest` 401. Over `http://` the right and wrong password and no login are all 403 with no `WWW-Authenticate` (not 401). Without `--auth` the https 401 lists three Digest then Basic then Bearer.
- Surl.Api: `AuthenticationMethods = ["basic"]` (a null `IList<string>`; a collection initializer throws NullReferenceException), `SelfSigned = true`, `SelfSignedCertificatePem` as `CurlTlsPeerOptions.CertificateAuthorityBlob` keeps verification on for https. `BoundUrls` are `Uri`. `CurlResponse.Headers` is a list of `KeyValuePair<string,string>`.
- surl-mcp `start_server` with `arguments: ["--cert","server.pem","--key","server.key","--auth","basic","--user",...]` reads the files from `SURL_MCP_ROOT`; curl-mcp `transfer` has no user/CA options, use the `curl` tool (`--cacert server.pem`, root `CURL_MCP_ROOT`).

## Which account did what (log, events, exchanges)
- Log: only `-v` names accounts. Lines: `#N * Login accepted: Digest billing`, `#N * Login refused: Digest search` (the claimed name), then `#N * GET /x: 200, 14 bytes of <disk path>` (in memory, a made-up `C:\surl\x` path on Windows), or `GET /x: 401, a login is needed` (also the first round of every Digest fetch). Info level: `Exchange N opened` lines only, no login lines. `#N` is the exchange = one connection. `--log-file f` appends; readable right after curl returns (no flush wait needed). verify's ready-port probe is exchange 1 on a console server.
- Passwords never appear in the HTTP/1.1 `-v` log, but over h2 `-v` logs DATA frames, so a served body (a token file) does appear.
- A Bearer login's account is `bearer token` everywhere (log, events), never the token.
- Surl.Api: `RecordExchanges` defaults to false; then `FindEvents` throws `InvalidOperationException`. `FindEvents(new SurlEventQuery { Kind = SurlEventKind.LoginJudged })` returns `LoginJudgedEvent` (namespace `Surl.Protocol.Abstractions`): `ExchangeId`, `Mechanism` (`Digest`, `Basic`, `Bearer`), `Account`, `IsAccepted`. `FindExchanges(new SurlEventQuery { Account = x })` returns `SurlExchangeRecord` (`ExchangeId`, `Received` bytes) of every exchange that judged a login for x, refused ones included. One `CurlClient` reuses its connection, so two accounts through one client land in one exchange. `Log = new StringWriter()`, `LogLevel = LogLevel.Verbose` (`using Surl.Output;`).
- surl-mcp records always. `get_events(kinds: ["login_judged"])` events: `{sequence, at, kind, exchange_id, scheme, account, mechanism, is_accepted, is_refused_by_fault}`; with `account: x` it keeps x's exchange_started, login_judged, exchange_ended; an unknown account answers `events: []`, not an error. `get_exchanges(after_exchange_id: N-1, max_exchanges: 1)` picks exchange N; `chunks[{direction: received|sent, data}]`. Each curl-mcp `curl` call is its own process and connection. curl-mcp's libcurl writes Digest `Authorization` in the Linux layout even on Windows.

## Tokens
- surl issues no tokens: the token is an account (`--user :tok`), served as a file for a "token endpoint"; accounts are not scoped to paths, so the password alone also reaches the "API". Basic over https: `curl -u` (no method) sends it preemptively; https 401 offers 3 Digest, `Basic realm="surl", charset="UTF-8"`, `Bearer realm="surl"`.
- Curl.Api Bearer: `new CurlAuthenticationOptions { XOAuth2Bearer = token, HttpAuthentication = HttpAuthenticationMethods.Bearer }` (`using Curl.Api.Options.Authentication;`). Basic: `HttpAuthentication = HttpAuthenticationMethods.Basic`.

## Recipes
http-accept-basic-only-over-https (and its twins), http-log-in-with-digest, http-log-in-with-ntlm, http-require-a-login, http-refuse-a-password-in-clear, http-tell-clients-apart-by-account (and its twins), http-exchange-a-password-for-a-token (and its twins).
