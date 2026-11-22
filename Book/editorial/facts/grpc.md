<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# grpc facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-20, windows.

## Listen URLs and readiness
`grpc` (HTTP/2 prior knowledge, plain) and `grpcs` (TLS, needs `--cert` or `--self-signed`); TCP, `ready: port`. Built in: `surl.test.Echo` (`Echo`, `Expand`, `Collect`, `Chat`) and server reflection `v1` and `v1alpha`. Reflection cannot be turned off, so a server "with no reflection" cannot be built. `echo.proto` is printed by `surl --aihelp grpc` (also a fixture of grpc-call-a-method).

## Server options that matter
- `--allow-anonymous` (loosens, warns): calls with no credentials. See grpc-call-a-method for `--user`, `--allow-plaintext-auth`.
- Request fields of `EchoRequest`: `message` 1, `count` 2, `status_code` 3, `status_message` 4, `delay_milliseconds` 5. JSON names `statusCode`, `statusMessage`, `delayMilliseconds`.

## Client: curl
- `grpc://host:port/<pkg.Service>/<Method>`, request `-d '{...}'` JSON; reply printed as JSON, one object per message, default values left out (`index` 0).
- `--grpc-proto file.proto`: types from the file, no reflection. The file decides the JSON names (field numbers on the wire). Errors: unknown field `curl: (2) -d: the data is not a surl.test.Ask message as JSON (UnknownField)` (exit 2); method not in the file `--grpc-proto: service surl.test.Echo has no method Chat` (2); missing file exit 26 `File "missing.proto" was not found`; syntax error exit 26 `bad.proto:1:28: Expected a service name, found "{".`; a service the server does not have, with a file: `grpc-status 12 (UNIMPLEMENTED): unknown service`, exit 22.
- Any `grpc-status` other than 0: exit 22, stdout the replies already sent, stderr `curl: (22) grpc-status 5 (NOT_FOUND): no such order`. Empty message: `grpc-status 14 (UNAVAILABLE): `. `statusCode` outside 0..16 (99, -1): `grpc-status 3 (INVALID_ARGUMENT): request field out of range`. `grpc-message` percent-encoding is undone (`disk 100% full`).
- `-m 1` with `delayMilliseconds` 3000: exit 28, no status. The text includes the elapsed milliseconds; match `curl: (28)`. `-m` is also sent as `grpc-timeout`, which surl enforces, so the server can answer `grpc-status 4 (DEADLINE_EXCEEDED): deadline exceeded`, exit 22, first (seen on macOS); add `-H "grpc-timeout:"` to leave the header off (see platforms.md).
- With no body, `GET`: curl without `-d` calls with an empty request; reply `{}` for Echo.

## MCP
surl-mcp `start_server(listen_urls ["grpc://127.0.0.1:0"], arguments ["--allow-anonymous"])`. curl-mcp `curl(args)` as the console; a failing status is a tool error (`isError` true) with `exitCode` 22 and `standardError`; `.proto` files go in `CURL_MCP_ROOT` (recipe fixtures). `get_events` gives `exchange_ended` with `sent_byte_count`; `get_exchanges` gives base64 HTTP/2 frames with compressed headers.

## C#
- Surl.Api hosts `grpc://127.0.0.1:0`; `BoundUrl.Port` for the framework client: `HttpClient` with `Version = 2.0`, `VersionPolicy = RequestVersionExact`, a `POST` of `[0, 4-byte big-endian length, message]`, `Content-Type: application/grpc`, header `TE: trailers` (the server answers 13 `te: trailers required` without it). Status in `response.TrailingHeaders` (`grpc-status`, `grpc-message`), or in `Headers` when the call fails before a reply.
- Curl.Api 0.2.0: `GET grpc://.../surl.test.Echo/Echo` = reflection call with an empty request, body `{}`. A `POST` body was not sent (status 0, `{}`) before Curl 0680b97577; since then a `POST` body IS the request message: `{"message":"hello"}` over grpcs is echoed back as indented JSON (`{\n  "message": "hello"\n}`); a `GET` still sends the empty message, `{}`. With `Content-Type: application/json` it fails `HTTP status 415`. No option for a `.proto` file. Unknown service `CurlException` 22 `HttpReturnedError`; unknown method 2 `FailedInit`.
- Libcurl 0.2.0: `UnsupportedProtocol` for `grpc://` (and `kafka://`).

## Gotchas
- Reflection request by hand: `ServerReflectionRequest.list_services` is field 7 (`3A 00`); the answer's field 6 holds the services.

## Recipes
grpc-call-with-a-proto-file, grpc-test-a-failing-call (each with -in-csharp and -from-an-agent); also grpc-call-a-method.

- Libcurl answers `UnsupportedProtocol` for this scheme by design: Libcurl mirrors upstream libcurl's protocol set, and extensions never change Libcurl or `curl -V`. Use Curl.Api or the console (Curl session, 2026-11-20).

## grpcs (TLS) and logins (grpcs-call-over-tls, Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20, windows)
- `surl --cert server.pem --key server.key --user alice:s3cret grpcs://...`; curl `-s -S --cacert server.pem -u alice:s3cret -d '{"message":"hello"}' grpcs://.../surl.test.Echo/Echo` prints the reply. No `--cacert`: exit 60 (schannel wording on Windows; match `curl: (60)`); URL host `localhost` for an IP-only certificate: 60. `-k` also works. No cert: surl exit 58 `needs a certificate`.
- Logins are judged at curl's first request, the reflection call: wrong password or none, exit 22 `The server did not answer gRPC server reflection (grpc-status 16 (UNAUTHENTICATED): unauthenticated); give the method's .proto file with --grpc-proto`. Over plain `grpc://` with an account, `-u` is refused unchecked: same wording with `grpc-status 7 (PERMISSION_DENIED): permission denied`.
- Plain `grpc://` to a grpcs port: exit 55 `Send failure: Connection was reset` (Windows only probed).
- Curl.Api (re-pin, unchanged): `GET` and `POST` with a JSON `Body` both reply `{}` (the body is not sent); `Content-Type: application/json` gives `HTTP status 415` as grpc-status 2. TLS via `CurlTlsPeerOptions.CertificateAuthorityFile`, login via `CurlAuthenticationOptions { UserName, Password }`; failures `PeerFailedVerification` (60), `HttpReturnedError` (22) with the same reflection wording. Surl.Api without `CertificateFile`: `SurlServerStartException` `CertificateProblem` (58).
- surl-mcp `start_server(["grpcs://127.0.0.1:0"], arguments ["--cert","server.pem","--key","server.key","--user","alice:s3cret"])`; no cert: tool error `exit_code` 58 `CertificateProblem`, text `surl: (58) grpcs://127.0.0.1:0/ needs a certificate: ...` (port 0 kept). curl-mcp `curl` tool takes the console's args.
- Recipes: grpcs-call-over-tls (+ -in-csharp, -from-an-agent).

## Streams and deadlines (grpc-read-a-server-stream, grpc-time-out-a-slow-call + twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `Expand`: `count` 0 or absent = no replies, exit 0, empty output; `count` above 1000 (1001) = `grpc-status 3 (INVALID_ARGUMENT): request field out of range`, exit 22, no replies. `delayMilliseconds` holds back EACH reply (count 3, 500 ms: total about 1.7 s, replies about 0.5 s apart), so replies are printed as they arrive; `-m 1 -H "grpc-timeout:"` with count 10 and 300 ms leaves some, not all, replies on stdout, exit 28.
- `grpc-timeout`: `-m N` is sent to the server; `-H "grpc-timeout: 300m"` (or `1S`) is enforced by surl: `grpc-status 4 (DEADLINE_EXCEEDED): deadline exceeded`, exit 22, with `-m 10` not yet passed; on a stream the replies before the deadline are printed first. With surl `-v --log-file f`, the lines `* gRPC call /... metadata: grpc-timeout, te, ...` name the header: two per curl call made with `-m` (reflection, then the method), none with no `-m` or with `-H "grpc-timeout:"`. surl-mcp refuses `--log-file` (use the console or Surl.Api `Log` + `LogLevel.Verbose` for this).
- Curl.Api: `CurlClientOptions.Timeout` is `-m` and sends `grpc-timeout` too; `Timeout` 1 s against a 3 s hold-back: `CurlException` `OperationTimedOut` 28 `Operation timed out after 1011 milliseconds with 0 bytes received`. `RemovedHeaders = ["grpc-timeout"]` is `-H "grpc-timeout:"`; a `Headers` entry `new("grpc-timeout", "")` is sent empty and refused `INTERNAL malformed grpc-timeout` (reflection lookup fails, HttpReturnedError 22). A `Headers` `grpc-timeout: 300m` gives HttpReturnedError 22 `grpc-status 4 (DEADLINE_EXCEEDED): deadline exceeded`. A stream ending in a non-OK status (deadline, `statusCode` 13) throws and the earlier replies are NOT returned (console and curl-mcp keep them in stdout). The `Body` stream is filled as replies arrive (read cancelled at 1 s: some of ten).
- Without `-H`, `-m 1` against 3 s: curl-mcp on Windows exit 28 (agent recipe accepts 28 or 22).
- Recipes: grpc-read-a-server-stream, grpc-time-out-a-slow-call (each with -in-csharp and -from-an-agent).
