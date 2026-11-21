<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# ftp facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows; Curl.Api FTP file time and TFTP upload re-probed on Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20. Also covers tftp where marked.

## Listen URLs and readiness
`ftp://127.0.0.1:{{port}}/`, TCP, default port 21, `ready: port`; `ftps` needs `--cert` or `--self-signed`. `tftp://127.0.0.1:{{port}}/` is UDP, `ready: none`.

## Server options that matter
- `--allow-anonymous`: curl's anonymous login is accepted, nothing is checked; loosens; warns on start. Without it (and no `--user`) every login is 530, curl exit 67, whatever the mode or command.
- `--allow-uploads`: STOR, DELE, MKD ...; loosens, no start-up warning (only `--allow-anonymous` warns). Without it a curl `-T` is exit 25; for tftp exit 69.
- `--max-filesize <bytes>`: an upload past it is refused 552, curl exit 70, nothing kept.
- `--directory`: two surls cannot share one directory (exit 124, `DataDirectoryInUse`); the second server of a recipe runs in memory.
- `-v --log-file site/surl.log`: the log lands in the served directory and can be read back over FTP; it holds `Data connection opened: passive` / `active to 127.0.0.1:<port>` after each RETR.

## Secure by default
No login without an account or `--allow-anonymous`: 530, curl 67. A password over plain `ftp://` is refused unchecked without `--allow-plaintext-auth`. Dot files are not served: curl 78 (ftp), 68 (tftp).

## Client: curl
- Passive is the default: `EPSV` (reply `229 ... (|||port|)`), `--disable-epsv` sends `PASV` (`227 ... (127,0,0,1,p1,p2)`). Active: `-P -` sends `EPRT |1|127.0.0.1|port|` (`200`), `-P - --disable-eprt` sends `PORT 127,0,0,1,p1,p2`. `-P <address>` names that address.
- surl accepts active only to the client's own address and a port from 1024 up: `EPRT |1|192.0.2.1|..` is `501 Address must be your own, port 1024 or above`; curl falls back to PORT, `501` again, exit 30.
- `curl -I ftp://.../file` sends MDTM then SIZE (no EPSV, no RETR) and prints `Last-Modified`, `Content-Length`, `Accept-ranges: bytes`; a missing file is exit 78 with no output. `-R` gives the local copy the server's MDTM time (UTC in the reply; touch -t in setup is local time, so a console claim on the day allows 1st to 3rd).
- `-w %{filetime}` is not known to this curl port.
- Exit codes: 67 login denied, 78 file not found, 30 FTP port failed, 25 upload failed, 70 remote disk full, 21 quote command failed (MDTM on a missing file). Upload sends `TYPE I` then `STOR`.
- Console recipes cannot read surl's stderr; `--log-file` into the served directory, then `curl ftp://.../surl.log`, is how a case checks the server's log.
- Setup `touch -t 202401021504.05 site/hello.txt` works under verify on Windows (Git touch) and gives a fixed-ish date; the `sh` fixtures compare mtimes with `[ a -ot b ]`.

## MCP
- `start_server(listen_urls: ["ftp://127.0.0.1:0"], arguments: ["--allow-anonymous", "--directory", "site", "--allow-uploads", "--max-filesize", "100"], files: {...})`. `--directory site` is relative to `SURL_MCP_ROOT`.
- curl-mcp `curl(args)`: `{exitCode, standardOutput {content}, standardError {content}, failure {kind CurlExit, text "curl exit code 25 (UploadFailed)"}, isError}`. Use `-s` (progress meter otherwise fills standardError), `-v` for the commands. Line ends of `-I` output are CR LF.
- curl-mcp `transfer(url)` also reaches `tftp://` (`statusCode` 0, body in `body.content`); it has no FTP options, so a mode is chosen with `curl` and `-P`.
- surl-mcp `get_exchanges(server_id, after_exchange_id)`: `exchanges[].chunks[] {direction sent|received, layer wire, encoding text|base64, data}`; ftp is text, one exchange per control connection (so one curl call each), tftp is base64 packets (`AAFoZWxsby50eHQAb2N0ZXQA...` is RRQ hello.txt octet). Data-connection bytes are in the chunks too. `get_events` has only exchange_started/ended, no data-connection kind.
- surl-mcp `read_file(server_id, path "/x")`: `{byte_count, content_type, text | base64}`; error "No file at /x" for a missing one; the path must start with `/`. `put_file(server_id, path, base64)` overwrites.
- curl-mcp `version()`: `{version, protocols[], extendedProtocols[], features[], tlsBackend, versionLine}`; `protocols` lacks `ipfs ipns` that `curl -V` lists; `start_server` with an unknown scheme is a tool error.

## Curl.Api / Surl.Api
- `CurlFtpOptions { UseEpsv, UseEprt, ActiveAddress ("-" or an address), Quote [..] }` on `CurlRequest.Ftp`. Every control-connection reply is a `Headers` entry whose Key is the reply line (`213 20240102150405`) and Value empty.
- `CurlClient.Create(new CurlClientOptions { FetchFileTime = true })` (Curl 7d167fa10a, BL-5603) is `-R`: `HttpMethod.Head` on an FTP URL then sends `MDTM`, `TYPE I`, `SIZE`, `REST 0` (no EPSV, no RETR), and `Headers` holds `Last-Modified` (`Tue, 02 Jan 2024 15:04:05 GMT`, parse with `"r"`) and `Content-Length`, as `curl -I`; a missing file is `RemoteFileNotFound` 78 (with an MDTM quote instead it was `QuoteError` 21). Without it a HEAD sends SIZE only. `CurlResponse.Info.FileTime` stays -1 even with FetchFileTime on (README says it holds the time; reported): read `Last-Modified`.
- Failures are `CurlException` (`Curl.Api.Errors`) with `Code`: `LoginDenied` 67, `RemoteFileNotFound` 78, `FtpPortFailed` 30, `QuoteError` 21.
- One `CurlClient` reuses its control connection, so several requests are one exchange; make a client per fetch to get one exchange each.
- `SurlServerOptions`: `DataDirectory` and `Files` cannot both be set; `Log` (TextWriter) with `LogLevel.Verbose` (`using Surl.Output;`) gets `Data connection opened: passive`.
- `File.SetLastWriteTimeUtc` on a served file gives an exact MDTM.

## Gotchas
- Python heredocs through the Bash tool collapse `\\` to `\`: write files with `\n` escapes (YAML, C#) with the Write tool, not a heredoc.
- `stdout-matches` is applied with `re.S`; `curl -I` prints `\r\n`.

## Recipes
ftp-choose-passive-or-active-mode (+ -in-csharp, -from-an-agent), ftp-read-a-files-size-and-date (+ -in-csharp, -from-an-agent), ftp-check-an-upload-landed-intact, tftp-download-a-file-from-an-agent, ftp-append-to-a-file, ftp-create-folders-while-uploading, ftp-rename-and-delete-a-file (each + -in-csharp, -from-an-agent)

## Appending, folders, rename and delete (ftp-append-to-a-file, ftp-create-folders-while-uploading, ftp-rename-and-delete-a-file; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20)
- `curl -T f --append url/x` sends `TYPE I` then `APPE x`, `150`, `226`; a missing remote file is created; only the bytes sent are added (no newline fixed). Uploads off: `APPE` is `550 Not permitted`, exit 25. `--max-filesize N` counts the file after the append: 11 bytes stored + 12 sent under 15 is exit 70, file unchanged.
- `--ftp-create-dirs`: `CWD` each folder, `MKD` only after `550 No such directory`, then `CWD` again (`257 "/2026/11" created`); existing folders get `250` and no `MKD`. Without it a missing folder is exit 9 (`550` to CWD), nothing made. Uploads off: `MKD` is `550 Not permitted`, exit 9. Listing a folder needs `--list-directories` (else `550 No such directory`, exit 19).
- `curl -Q "RNFR a" -Q "RNTO b" url/` (no prefix) runs before the listing; `-Q "-DELE x"` runs after it. Replies: `RNFR` 350 `Ready for RNTO`, `RNTO` 250, `DELE` 250; missing file `550 No such file`; `RNTO` alone `503 Send RNFR first`; `RNTO` into a missing folder `553 No such directory`; uploads off `550 Not permitted`; all exit 21. Rename onto an existing name replaces it. A quote needs a transfer: use a folder URL (`-l`, or `-o` null device).
- Console shell on Windows verify/probe is cmd: `2>&1` works in a case, a `|` pipe to grep with `>` in its pattern does not. `{null}`/`{{null}}` for the null device.
- Curl.Api: `CurlRequest(HttpMethod.Put, url) { Body = bytes, Ftp = new CurlFtpOptions { Append = true } }` is `-T --append`; Put without `Append` is `STOR`. `CreateMissingDirectories = FtpCreateMissingDirectories.Create` (`using Curl.Api.Options.Protocols;`; also `None`, `CreateAndRetry`) is `--ftp-create-dirs`. `Quote` is `-Q` (before), `PostQuote` is `-Q "-cmd"` (after); both `IReadOnlyList<string>`, collection expressions work. GET on the folder URL (`server.BoundUrl.ToString()`, a Uri) returns the long listing in `Body`.
- Exception codes: `UploadFailed` 25, `RemoteDiskFull` 70, `RemoteAccessDenied` 9, `QuoteError` 21, `LoginDenied` 67.
- Surl.Api: `MaxFileSize`, `AllowUploads`, `ListDirectories`; `server.Files.ReadAsync("/x")` (`.Bytes`), `server.Files.ListAsync("/dir")` (`.Entries`, `Name`, `IsFolder`), `Files["2026/old.txt"]` seeds a folder; ListAsync needs no `ListDirectories`.
- surl-mcp `list_files` answers `entries[] {name, kind folder|file}`; `read_file` `text`. Each `curl` tool call is its own exchange in `get_exchanges`; `received` chunks begin with the command line (`APPE log.txt`, `MKD 11`, `STOR x`).
- Cases share server state in order; a case may check `files:` under the served directory (`site/x`) after an upload.

## TFTP beside HTTP (tftp-move-a-firmware-fetch-to-http)
- One surl serves `tftp://127.0.0.1:{{port}}/ http://127.0.0.1:{{port}}/` from one `--directory` (or one `Files` store): UDP and TCP port numbers are separate, so one number does; `ready: port` waits on the TCP side. Same bytes either way; 1300 and exactly 1024 bytes (empty final block) both arrive whole; `--tftp-blksize 1024` and `--tftp-no-options` do not change the bytes.
- Exit codes: missing tftp file 68, missing http file with `-f` 22; tftp `-T` refused 69, http PUT 401 (22 with `-f`).
- curl-mcp `transfer` on tftp: statusCode 0, body base64 when binary; missing: failure kind `CurlExit`, text `TFTP: File Not Found` + newline + `curl exit code 68 (TftpNotFound)`. `curl` tool `-T up.txt tftp://...` exitCode 69.
- Curl.Api: `CurlRequest.Tftp = new CurlTftpOptions { BlockSize = 1024 }`; missing file throws `CurlException`, `Code` `TftpNotFound` (68). A PUT with a body to tftp:// is a TFTP write (Curl 7d167fa10a, BL-5594): uploads off, `CurlException` `TftpPerm` 69 `TFTP: Access Violation`; with `AllowUploads` it stores (StatusCode 0). Before, it was sent as a read request (68).
- Fixtures are text; a binary image is made by `setup: head -c 1300 /dev/urandom > site/firmware.bin` (needs schema 2) and compared with `cmp a b && echo identical`.

- Curl.Api has Unix sockets (`CurlNetworkOptions`) and the TFTP block size (`CurlTftpOptions`) (Curl session, 2026-11-20).

## TLS from Curl.Api (http-rotate-a-password-without-downtime-in-csharp, Curl 7d167fa10a, 2026-11-20, windows)
- `CurlNetworkOptions { UseSsl = UseSsl.All }` (`using Curl.Api.Options.Network;`, on the request or the client) does NOT send AUTH TLS on ftp://: the password goes in clear and surl answers `530 Login needs TLS first: send AUTH TLS`, `LoginDenied`. Console `curl --ssl-reqd` works. Use an `ftps://` listener (`SelfSigned = true` covers it) from Curl.Api. Reported.
- Curl.Api FTPS downloads fail about 1 in 30 with `PartialFile` (18) `server did not report OK, got 426` (surl: `The client closed the data connection before the whole file was sent`); 120 console curl downloads all passed. A HEAD (SIZE only, `Content-Length` in `Headers`) logs in with no data connection and is stable. Reported.

## FTP to S3 hop (ftp-copy-a-report-from-ftp-to-s3 and twins, Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20)
- `surl --cert server.pem --key server.key --user u:p ftp://H:P/` (no loosening) + `curl --ssl-reqd --cacert server.pem -u u:p` works (AUTH TLS); without `--ssl-reqd` the login is 530, exit 67. CRLF bytes arrive unchanged; `-B` changed nothing on Windows (not claimed).
- `curl ... -o f ftp://.../missing --next ... -T f s3://...`: the download fails 78 but curl goes on and uploads whatever `f` already holds (a stale file), exit 0 (last transfer's). `--fail-early` stops at the 78. If `f` does not exist the upload is exit 26.
- verify's `files:` reads text (CR LF folded to LF): show byte identity with `cmp a b && echo identical` (Git's cmp works under cmd).
- Curl.Api: `CurlNetworkOptions.UseSsl = UseSsl.All` (`Curl.Api.Options.Network`) on an `ftp://` URL did NOT upgrade: the server saw `USER`/`PASS` in clear, 530 LoginDenied (finding reported). An `ftps://` listen URL (implicit TLS) with `CertificateFile`/`KeyFile` and Curl.Api `Tls` trust works; `ListenUrls = { "ftps://...:0", "ftp://...:0" }` gives both, `BoundUrls[1]` the plain one. Missing file: `RemoteFileNotFound`.
- surl-mcp `start_server` with `--cert server.pem --key server.key` in `arguments` (relative to SURL_MCP_ROOT) works for ftp and s3; curl-mcp `curl` with `--ssl-reqd ... --next ... -T` works; `read_file` text keeps CR LF.

- Do not use Curl.Api `UseSsl.All` (or Try/Control) in a recipe until Curl BL-5640 lands: on `ftp://` it sends USER and PASS in clear with no AUTH TLS, and on `imap://` the STARTTLS login fails 67. Use the implicit-TLS scheme (`ftps://`, `imaps://`, `pop3s://`, `smtps://`) instead (Curl session, 2026-11-20).
