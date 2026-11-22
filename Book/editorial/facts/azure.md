<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# azure facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`az://127.0.0.1:{{port}}/` is TLS from the first byte, TCP, `ready: port`. Needs `--cert`/`--key`: without, exit 58, `surl: (58) az://127.0.0.1:0/ needs a certificate...`. Can share a store with an `https://` listener in the same surl: the blob `photos/note.txt` is `/photos/note.txt` there, same token.

## Server options that matter
- `--directory store`: each top-level folder a container, the path under it the blob name; files already there are blobs.
- `--user devstoreaccount1:<base64 key>`: a storage account; its password is the Shared Key. Path style (`/<account>/<container>/<blob>`) strips the first segment only when it names an account surl holds; otherwise it is the container (404 ContainerNotFound). Kept on purpose (Surl ADR-0109; cite the Surl README).
- `--user :<token>`: an empty name makes the password a Bearer token.
- `--allow-uploads`: without it a put is 403 AuthorizationPermissionMismatch; gets still served.
- `--allow-anonymous` (loosens, warns): gets with no token served; a put with no token is still 401 NoAuthenticationInformation.
- Listing a container needs `--list-directories` (kept on purpose, ADR-0109); without it, 403 AuthorizationPermissionMismatch (even with the token). With `--allow-anonymous` and `--list-directories`, an unsigned listing is served. A missing container: 404 ContainerNotFound.

## Secure by default
No token: 401 NoAuthenticationInformation. Unknown token: 401 InvalidAuthenticationInfo. Wrong Shared Key: 403 AuthenticationFailed. Missing blob: 404 BlobNotFound. For each, curl writes nothing on stdout, exits 22, stderr `Azure Storage returned error <status>: <Code>`.

## Client: curl
- `az://<IP or localhost>:<port>/<account>/<container>/<blob>` is emulator path style; curl speaks HTTPS; `--cacert server.pem`.
- `--oauth2-bearer <token>` sends Bearer. `-u <account>:<base64 key>` signs with Shared Key (new in b9ca725077; before, it sent nothing and got 401). A key that is not base64: exit 67, `The account key is not base64.`
- `-T file` puts; a URL with no `-T` gets to stdout, so one command can put then get.
- A URL ending in `/` (`.../devstoreaccount1/photos/` or `.../photos/2026/`) lists: curl sends List Blobs with `delimiter=/` and the path as `prefix`, prints names one to a line, folders with a trailing `/`, sorted. A query string is dropped. Empty prefix: no output, exit 0. `.../devstoreaccount1/` alone: exit 3 `An az:// URL needs a container after the account`. Works with `--oauth2-bearer` and with `-u account:key`.

## Curl.Api (C#) and MCP
- Surl.Api: `ListenUrls = { "az://127.0.0.1:0" }`, `CertificateFile`/`KeyFile`, `Accounts.Add(new SurlAccount("devstoreaccount1", "c2VjcmV0"))` and `new SurlAccount("", "t0ken-abc")` for the Bearer token, `Files["photos/a.txt"]`, `ListDirectories`, `AllowAnonymous`. Curl.Api: `new CurlRequest(HttpMethod.Get, $"{server.BoundUrl}devstoreaccount1/photos/2026/")` (BoundUrl is `az://127.0.0.1:port/`), `Authentication = new CurlAuthenticationOptions { XOAuth2Bearer = "t0ken-abc" }` or `{ UserName, Password }` (Shared Key), `Tls` with `CertificateAuthorityBlob` from an openssl cert for 127.0.0.1. A refusal throws `CurlException` 22 `HttpReturnedError`, `Message` `Azure Storage returned error 403: AuthorizationPermissionMismatch: ...` (extra lines `RequestId:`/`Time:`: print the first line). Curl.Api cannot PUT to `az://` (`NotBuiltIn`, 4), as for `s3://`.
- surl-mcp: `start_server` `arguments` `--cert server.pem --key server.key --user devstoreaccount1:c2VjcmV0 --user :t0ken-abc --list-directories`; `list_files` path `/photos/2026`. curl-mcp `curl` tool runs `--oauth2-bearer t0ken-abc az://127.0.0.1:port/devstoreaccount1/photos/`.

## Gotchas
- probe.py has no setup step: pass a certificate through PROBE_FILES (newlines escaped) or case it in recipe.yml and run verify.
- `--az-plaintext`, `abfs`/`abfss`: not probed.

## Recipes
az-put-and-get-a-blob, az-list-a-container (and twins)

## Plain HTTP (az-test-an-azure-upload-locally and twins, Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `--az-plaintext` serves `az` as plain HTTP/1.1, no certificate; warns `surl: warning: --az-plaintext: az is served as plain HTTP, without TLS`. A Bearer token over it needs `--allow-plaintext-auth` too (warns); without it the right token is 401 InvalidAuthenticationInfo. Without `--az-plaintext` and with no cert: exit 58.
- curl's `az://` is TLS only (exit 35 `SEC_E_INVALID_TOKEN` against the plain listener), so a plain-HTTP client is plain `curl -H` over `http://127.0.0.1:P/<container>/<blob>` (no account segment with a token-only server): put `-T note.txt -H "x-ms-version: 2023-11-03" -H "x-ms-blob-type: BlockBlob" -H "Authorization: Bearer T"` is 201 Created (also 201 on replace); either header missing is 400 MissingRequiredHeader. The put answer has `Content-MD5` (base64 MD5 of the body). Errors are in the `x-ms-error-code` header: `curl -w "%{http_code} %header{x-ms-error-code}"`.
- Get 200; DELETE 202; List Blobs `?restype=container&comp=list` (quote the URL) needs `--list-directories`, XML with `<Name>`. No token 401 NoAuthenticationInformation; wrong token 401 InvalidAuthenticationInfo; uploads off 403 AuthorizationPermissionMismatch; missing blob 404 BlobNotFound.
- Surl.Api: `AdditionalArguments = { "--az-plaintext" }`, `AllowPlaintextAuthentication = true`, `Accounts.Add(new SurlAccount("", "t0ken"))`; the plain address is `http://127.0.0.1:{BoundUrl.Port}`. Curl.Api sends `Headers = [new("x-ms-version", ...)]` (list of KeyValuePair) and `Body = CurlContent.FromBytes(...)`; a refusal is a response. surl-mcp `start_server` `arguments` the same words; a start refusal is a tool error with `exit_code` 58 and `surl: (58) az://... needs a certificate: give --cert <file>...`. curl-mcp `transfer` takes `headers` as a JsonArray of `{name, value}` (the SDK serializer refuses anonymous types and `List<object>`).
