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
http-accept-basic-only-over-https (and its twins), http-log-in-with-digest, http-log-in-with-ntlm, http-require-a-login, http-refuse-a-password-in-clear, http-tell-clients-apart-by-account (and its twins), http-exchange-a-password-for-a-token (and its twins), http-sign-requests-with-aws-sigv4 (and its twins), http-lock-out-one-account (and its twins), surl-share-one-account-file (and its twins).

- Decided by Surl (2026-11-20): `--auth basic` over plain `http://` answers 403 with no `WWW-Authenticate`, because a 401 must carry a challenge and offering Basic in clear invites the password (Surl ADR-0032 step 5).

## AWS SigV4 over http (http-sign-requests-with-aws-sigv4, 2026-11-20, windows)
- `--user AKIAEXAMPLE:topsecret --auth aws-sigv4` over plain `http://`: `curl --aws-sigv4 "aws:amz:us-east-1:s3" -u KEY:SECRET` is 200. Wrong secret, unknown key, no signature, Basic `-u` and `--digest` are all 403 with no `WWW-Authenticate` (with `--auth aws-sigv4` alone nothing is offered). Missing file signed: 404. The scope (`eu-west-1`, `ec2`) is not checked: 200.
- No `--auth`: the same account also accepts the signed request (200); wrong secret or unknown key is 401 with the three Digest challenges (not 403).
- Log (`-v`): `#N * Login accepted: AWS4-HMAC-SHA256 AKIAEXAMPLE`, `Login refused: AWS4-HMAC-SHA256 <claimed key ID>`; the secret is never in it. Events: `Mechanism` `AWS4-HMAC-SHA256`, `Account` the key ID.
- Surl.Api: `AuthenticationMethods = new List<string> { "aws-sigv4" }`. Curl.Api: `CurlAuthenticationOptions { UserName, Password, AwsSigV4 = "aws:amz:us-east-1:s3" }`; Basic/Digest via `HttpAuthentication`. A 403 is a `CurlResponse`; `Headers` is a list of `KeyValuePair`.
- surl-mcp `start_server arguments: ["--user","KEY:SECRET","--auth","aws-sigv4"]`; curl-mcp `curl` tool `-s --aws-sigv4 aws:amz:us-east-1:s3 -u KEY:SECRET -o hello.out -w %{http_code}`; `transfer` (no login) answers statusCode 403 with `headers` a list of `{name,value}`. `-D -` in the `curl` tool puts the headers in `standardOutput`.
- The bibliography has no SigV4 entry; recipes cite `aws-s3-api`.

## Locking out one account (http-lock-out-one-account, 2026-11-20, windows)
- `--fault "refuse-login;account=bob"` (needs quotes; loosens, warns): bob's login is refused whatever the password: 401 with a fresh Digest challenge, same as a wrong password; alice and the rest carry on. `times=1` refuses once, then bob logs in (`remaining` 0 stays listed). Without the rule the same login works.
- Log (`-v`): `#N * Login refused: Digest bob`, `Login accepted: Digest alice`. Events: `LoginJudged` refused for bob; `fault_applied` events `rule_id` f1, `fault_kind` `refuse_login`.
- Console lift: `--control http://127.0.0.1:{{port3}} --control-token-file token.txt` (token 32 to 1024 visible ASCII on the first line, else exit 2). `GET /v1/faults` with `Authorization: Bearer <token>`: `{"faults":[{"fault_id":"f1","item":"refuse-login;account=bob","remaining":null}]}`; `DELETE /v1/faults/f1`: 204, bob logs in at once; no token: 401. `POST /v1/faults` wants `text/plain` (415 for JSON). The control port is not the ready port; `-X DELETE ... --next ...` chains lift and check in one case. A YAML `run:` with `Authorization: Bearer` needs single quotes.
- Surl.Api: `options.Faults.Add(SurlFault.RefuseLogin() with { Account = "bob", Times = 1 })`; `server.Faults.List().Single().Id` is `f1`, `Remove(id)` returns true. surl-mcp: `add_fault(server_id, fault: "refuse-login;account=bob")` -> `fault_id` f1, `remove_fault`, `list_faults`.

- Decided by Surl (2026-11-20): `--auth aws-sigv4` accepts any provider, region and service (Surl ADR-0043 decision 2). They are bound into the signature, not configured on the server, so surl answers whatever curl was told to sign for. State it as designed.

## One account file for four TLS protocols (surl-share-one-account-file + twins; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20, windows)
- `--user-file users.txt` (lines `user:password`, `#` comments and blank lines skipped, read at start) beside `-u`: one surl with `https://`, `ftps://`, `imaps://`, `mqtts://` (one `--cert`/`--key`, four ports) logs the same accounts in on all four. `curl --cacert server.pem -u alice:s3cret`: https GET 200 (Basic preemptive over TLS), ftps GET file, imaps `-X "STATUS INBOX (MESSAGES)"` prints `* STATUS INBOX (MESSAGES 0)` (a file account has an empty INBOX), mqtts `-d on` exit 0.
- Refused (missing user and wrong password alike): https 401 (`-f` exit 22), ftps 67, imaps 67, mqtts CONNACK 4 exit 8 `Expected 0000 but got 0004`. Curl.Api: https a `CurlResponse` 401, ftps/imaps `LoginDenied` 67, mqtts `WeirdServerReply` 8.
- Unreadable file exit 37 (`Could not read user file X`); a line without a colon exit 2 (`User file X, line 1: expected <user:password>`). `SurlServerOptions.UserFile` adds to `Accounts`; a bad file is `SurlServerStartException`; surl-mcp `start_server` is an `isError` with `exit_code` 2.
- surl-mcp `get_events(kinds:["login_judged"])`: one event per login, `account` and `is_accepted`; four curl-mcp calls (one per protocol) gave four events for alice, all accepted; refusals are under the claimed name.

## Changing accounts while the server runs (http-rotate-a-password-without-downtime + twins, http-rotate-a-password-in-process; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20, windows)
- One password per account name; an account logs in on every scheme of the server (https and ftp/ftps shared). Rotation without a refused login = a second account (app-v1, app-v2), then remove the old.
- Console: control endpoint `POST /v1/accounts` with `curl --json @file.json` (`{"user":..,"password":..}`; without application/json: 415) -> 204; `DELETE /v1/accounts/<user>` -> 204, unknown -> 404 `{"error":"not_found","message":"no account is named x"}`. POST for an EXISTING name REPLACES its password (204; old refused at once). Events: `GET /v1/events?kind=login_judged&account=x` (kind `store_changed` also works: `account_added`/`account_removed` with `name`).
- Surl.Api: `server.Accounts.Add(user, pw)` (existing name: ArgumentException), `Remove(user)` -> bool, `HasAccounts`. surl-mcp: `add_account` -> `{"user"}`; existing name: tool error `user app-v1 already has an account`; `remove_account` -> `{"user"}`, unknown: tool error `server s1 has no account x`.
- Removing the LAST account opens the server: GET with no login 200 (PUT still 401). Surl.Api logs `surl: warning: no account left; the server now needs no login`; surl-mcp answers `{"user","warning":"server s1 has no account left and now needs no login"}` plus a second text block (`.Single()` on text blocks throws); the control endpoint writes no warning at all.
- Removal refuses new logins only: an FTPS connection logged in before keeps working (Curl.Api client reuse). Over HTTP every request logs in, so the next request on the same connection is refused (one exchange, refused then accepted after a reread).
- Events: `LoginJudgedEvent.Scheme` `https`/`ftp`/`ftps`; FTP mechanism `password`; https Basic via `curl -u` (no method).

## Retrying a refused login (http-stop-a-client-hammering-a-refused-login + twins)
- `curl -f --retry 3` does not retry a 401 (`%{num_retries}` 0, exit 22); without -f exit 0; `--retry-all-errors` retries it (3 retries, 4 refused logins). Count with `grep -c "Login refused: Digest app"` on a per-server -v log, or LoginJudged events (`is_refused_by_fault` true for the rule).

## CurlHttpMessageHandler (http-test-an-httpclient-app-against-surl)
- `new HttpClient(new CurlHttpMessageHandler(curlClient)) { BaseAddress = server.BoundUrl }`; trust via `CurlClient.Create(new CurlClientOptions { Tls = ... })`. App-set `Authorization` headers (Basic, Bearer) pass through unchanged. `handler.Credentials = new NetworkCredential(u, p)` answers the challenge with Digest over https. A 401 is a response (`EnsureSuccessStatusCode` -> HttpRequestException StatusCode 401); untrusted certificate -> HttpRequestException, inner CurlException `PeerFailedVerification` (60).
- Recipes: http-rotate-a-password-without-downtime (+ twins), http-rotate-a-password-in-process, http-stop-a-client-hammering-a-refused-login (+ twins), http-find-who-failed-to-log-in-from-an-agent (get_events grouped by account/mechanism; `exchange_started` gives `client`), http-test-an-httpclient-app-against-surl.
