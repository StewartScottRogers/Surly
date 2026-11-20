<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# smb facts (smb and smbs)
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`smb://127.0.0.1:{{port}}/`, `smbs://...` (TLS from the first byte), TCP, default port 445, `ready: port`. One surl may take several listen URLs (`smb://` and `http://`) over one `--directory`.

## Server options that matter
- `--directory <dir>`: each folder directly in it is a share (first URL path segment); the rest is the path inside. In memory, `files` keys such as `docs/readme.txt` make the share `docs`; a share that does not exist makes curl exit 78.
- `-u user:pw` and `--auth ntlmv1`: curl logs in with NTLMv1 only; without `ntlmv1` in `--auth` every login is refused (curl 67). `--auth ntlm` does not accept it. `--auth` also governs HTTP on a shared surl: `--auth ntlmv1,digest` keeps HTTP Digest (`curl --digest -u`); `ntlmv1,basic` over plain http is 403 (needs `--allow-plaintext-auth`).
- `--allow-uploads` (loosens): `curl -T file smb://.../share/name` creates or replaces; exit 0, prints nothing. Without it exit 9 `Access denied`, nothing written.
- `--max-filesize <n>`: past it curl exits 25 (`Upload failed (at start/before it took off)`), the partial file is deleted.
- smbs: needs `--cert`/`--key` (PEM) or `--self-signed`, else surl exits 58 `needs a certificate: give --cert <file>, or --self-signed for a throwaway one`.

## Secure by default
Read-only shares; NTLMv1 not accepted; smbs without a certificate refused to start.

## Client: curl
- Download `curl -u alice:s3cret smb://host:port/share/file`; one path segment only is exit 3 (`missing share in URL path for SMB`); wrong password or none 67 `Login denied`; missing share, file or a folder 78.
- smbs: `--cacert server.pem` trusts a self-signed cert with `IP:127.0.0.1` SAN; without it exit 60 (`schannel: SEC_E_UNTRUSTED_ROOT` here: match `curl: (60)` only); `--self-signed` needs `-k`. Plain `smb://` to an smbs port: exit 56 (Windows; Linux not checked).
- curl output with `-T` is empty on success; the progress meter goes to stderr unless `-s`.

## MCP
`surl-mcp start_server`: `listen_urls` may hold both smb and http; options in `arguments` (words); `files` in memory. curl-mcp `curl` tool takes `args` (`-T report.txt` reads from `CURL_MCP_ROOT`); answer `exitCode`, `standardOutput`/`standardError` `{content}` or null. A refused smbs start is a tool error with `exit_code` 58, `exit_code_name` CertificateProblem.

## C#
- `SurlServerOptions`: `ListenUrls`, `DataDirectory`, `Accounts`, `AuthenticationMethods = ["ntlmv1","digest"]`, `AllowUploads`, `MaxFileSize`, `CertificateFile`, `KeyFile`, `SelfSigned`.
- Curl.Api 0.2.0 reads `smb://` and `smbs://` with a plain GET (`CurlAuthenticationOptions` user/password; SMB login needs no option) and fails as `CurlException` (`RemoteFileNotFound` 78, `LoginDenied` 67, `PeerFailedVerification` 60).
- Curl.Api 0.2.0 CANNOT upload over SMB: `HttpMethod.Put` (or Post) with a stream or byte body to `smb://` or `ftp://` is sent as a download (RemoteFileNotFound). Libcurl 0.2.0 with `Upload=1L`, `Readfunction` and `Infilesize`/`InfilesizeLarge` in any order stops: code 55 `SMB upload needs to know the size up front`. The console `curl -T` works. Recipes upload over HTTP PUT on a second listener instead.
- `Process.Start("curl")` on Windows finds System32 curl first, not the PATH one: do not shell out.

## Gotchas
- Cases in one recipe share the case root: a file one case uploads is there for the next. Give servers their own `--directory`.
- Two surl cannot share a `--directory` (lock): one surl, two listen URLs, or separate directories.
- Digest with HTTP PUT in Curl.Api works with a byte body (201 create, 204 replace; 405 uploads off, 413 over `MaxFileSize`).

## Recipes
smb-upload-to-a-share, smbs-read-a-share-over-tls (each with -in-csharp and -from-an-agent); also smb-read-a-share.
