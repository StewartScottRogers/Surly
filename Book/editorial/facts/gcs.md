<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# gcs facts
Checked against: surl 1.0.0 (Surl 9ad60d80c), curl (Curl 7d167fa10a), 2026-11-21, windows. Google Cloud Storage as `gs://`; the same store and accounts as `s3.md` (see also `gs-put-and-get-an-object`).

## Listen URLs and readiness
`gs://127.0.0.1:{{port}}/` is TLS from the first byte, TCP, `ready: port`. Needs `--cert`/`--key` (or `--self-signed`): without, exit 58, `surl: (58) gs://127.0.0.1:0/ needs a certificate...`. `--gs-plaintext` serves plain HTTP/1.1 (warns `surl: warning: --gs-plaintext: gs is served as plain HTTP, without TLS`; a bearer token over it also needs `--allow-plaintext-auth`; not probed). The listener also answers the JSON API under `/storage/v1/` (Bearer only; an HMAC signature there is 400); not used by recipes.

## Server options that matter
- `--directory store`: each top-level folder a bucket, the path under it the object. One surl holds a folder at a time: give each server its own folder.
- `--user GOOGTESTKEY:s3cret`: an HMAC access key and its secret (V4 `GOOG4-HMAC-SHA256`). `--user :ya29.t0ken`: an empty name makes the password an OAuth Bearer token. Both can be on one server.
- `--allow-uploads`: without it a put is 403 AccessDenied (HMAC and Bearer alike); gets still served.
- `--allow-anonymous` (loosens, warns): unsigned gets served; an unsigned put is still 401 AuthenticationRequired.
- Listing needs `--list-directories` (403 AccessDenied without, even signed).

## Secure by default
No login: 401 AuthenticationRequired. Unknown Bearer token: 401 InvalidAuthentication (`The provided authentication header is invalid.`). Unknown access key: 403 InvalidAccessKeyId. Wrong secret: 403 SignatureDoesNotMatch. Uploads off: 403 AccessDenied (`Access denied.`). Missing object: 404 NoSuchKey. For each, curl writes nothing, exits 22, stderr `curl: (22) Cloud Storage returned error <status>: <Code>: <message>`.

## Client: curl
- `gs://<bucket>/<object>` sends to `storage.googleapis.com:443`, path style: reach surl with `--connect-to ::127.0.0.1:{{port}}` and `--cacert server.pem`; the certificate needs `DNS:storage.googleapis.com` plus `IP:127.0.0.1`.
- `-u KEY:SECRET` signs (HMAC); `--oauth2-bearer TOKEN` sends Bearer. `-T file` puts, with either. A URL with no `-T` gets.
- `https://storage.googleapis.com/photos/x` with `--oauth2-bearer` works as a plain HTTP get or put. `--aws-sigv4 goog:goog:auto:storage` did not sign for it (200 with `MissingSecurityHeader`): do not use.

## Curl.Api (C#)
- `CurlRequest(HttpMethod.Get, "gs://photos/hello.txt")` with `Network.ConnectTo`, `Tls.Origin.CertificateAuthorityBlob`, and `Authentication = { UserName, Password }` (HMAC) or `{ XOAuth2Bearer }` (token). A refusal throws `CurlException` `HttpReturnedError` (22), `Message` `Cloud Storage returned error 403: SignatureDoesNotMatch: ...`.
- **Program finding:** `HttpMethod.Put` with a `Body` (byte or stream) to `gs://` throws `NotBuiltIn` (4) `gs:// does not support -X PUT; it sends only GET, or PUT with -T`, though the same put to `s3://` works (BL-5594) and console `curl -T` to `gs://` works. A C# put goes over `https://storage.googleapis.com/photos/x` instead, with `Authentication = { XOAuth2Bearer = t, HttpAuthentication = HttpAuthenticationMethods.Bearer }` (`using Curl.Api.Options.Authentication`): over https the token is sent only with `HttpAuthentication` Bearer. A refusal over https is a response (403 / 401, XML body with `<Code>`), not an exception. HMAC put in C#: no way found.
- Surl.Api: `ListenUrls = { "gs://127.0.0.1:0" }`, `CertificateFile`/`KeyFile`, `Accounts.Add(new SurlAccount("GOOGTESTKEY", "s3cret"))`, `new SurlAccount("", "ya29.t0ken")`, `AllowUploads`; `server.Files.ReadAsync("/photos/note.txt")` reads a put back, `SurlNotFoundException` when there is none.

## MCP
- surl-mcp `start_server` `arguments`: `--cert server.pem --key server.key --user GOOGTESTKEY:s3cret --user :ya29.t0ken [--allow-uploads]`. curl-mcp `curl` with `-sS --cacert server.pem --connect-to ::127.0.0.1:P -u K:S | --oauth2-bearer T [-T note.txt] gs://photos/x`: exitCode 22 and the error in `standardError.content`; `-T` works from `CURL_MCP_ROOT`. `read_file` (`path` `/photos/note.txt`, answer `text`) is a tool error for a put that was refused.
- `get_events kinds:["login_judged"]`: an HMAC login is `account` = the access key; a token is `account` = `bearer token` (not the empty name). No event for a request with no login. A refused HMAC put with uploads off gave no event, the token put did: assert the first few events only.

## Recipes
gs-put-and-get-an-object, gs-sign-with-an-hmac-key (+ -in-csharp, -from-an-agent)

## Reading as a file (gs-read-a-gcs-object-as-a-file and twins, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `surl --directory store --cert .. --key .. --user GOOGTESTKEY:s3cret --user :ya29.t0ken --allow-uploads gs://H:P1/ https://H:P2/`: a gs put is the file `store/<bucket>/<object>` and `https://H:P2/<bucket>/<object>` serves it (`Content-Type: text/plain` for `.txt`); a slash in an object name makes folders. https accepts `-u KEY:SECRET` (Basic) and `--oauth2-bearer`; no login is 401. HTTP/2 is fine here (no SigV4). One command `curl ... -T f gs://b/o --next ... https://...` writes then reads.
- **Resolved:** with Curl c84035f797 Curl.Api `HttpMethod.Put` with a `Body` to `gs://` works, answered 200, with `UserName`/`Password` (HMAC) and with `XOAuth2Bearer`; over `https://` name the method (`HttpAuthenticationMethods.Basic` or `Bearer`). The program finding above (NotBuiltIn) is out of date for this build. Surl.Api `BoundUrls[0]` is the gs port and `[1]` the https one, in listen order.
- curl-mcp: `-o got.txt -w "%{size_download}"` works (file written in `CURL_MCP_ROOT`); a refused gs put is exit 22 with `Cloud Storage returned error 403: ...`, a tool error.
