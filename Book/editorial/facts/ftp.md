<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# ftp facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows. Also covers tftp where marked.

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
- `CurlFtpOptions { UseEpsv, UseEprt, ActiveAddress ("-" or an address), Quote [..] }` on `CurlRequest.Ftp`. `HttpMethod.Head` on an FTP URL sends SIZE only; `Content-Length` appears in `Headers`; no MDTM unless `Quote = ["MDTM name"]`. Every control-connection reply is a `Headers` entry whose Key is the reply line (`213 20240102150405`) and Value empty. `Info.FileTime` stays -1 (no property asks for it).
- Failures are `CurlException` (`Curl.Api.Errors`) with `Code`: `LoginDenied` 67, `RemoteFileNotFound` 78, `FtpPortFailed` 30, `QuoteError` 21.
- One `CurlClient` reuses its control connection, so several requests are one exchange; make a client per fetch to get one exchange each.
- `SurlServerOptions`: `DataDirectory` and `Files` cannot both be set; `Log` (TextWriter) with `LogLevel.Verbose` (`using Surl.Output;`) gets `Data connection opened: passive`.
- `File.SetLastWriteTimeUtc` on a served file gives an exact MDTM.

## Gotchas
- Python heredocs through the Bash tool collapse `\\` to `\`: write files with `\n` escapes (YAML, C#) with the Write tool, not a heredoc.
- `stdout-matches` is applied with `re.S`; `curl -I` prints `\r\n`.

## Recipes
ftp-choose-passive-or-active-mode (+ -in-csharp, -from-an-agent), ftp-read-a-files-size-and-date (+ -in-csharp, -from-an-agent), ftp-check-an-upload-landed-intact, tftp-download-a-file-from-an-agent

## TFTP beside HTTP (tftp-move-a-firmware-fetch-to-http)
- One surl serves `tftp://127.0.0.1:{{port}}/ http://127.0.0.1:{{port}}/` from one `--directory` (or one `Files` store): UDP and TCP port numbers are separate, so one number does; `ready: port` waits on the TCP side. Same bytes either way; 1300 and exactly 1024 bytes (empty final block) both arrive whole; `--tftp-blksize 1024` and `--tftp-no-options` do not change the bytes.
- Exit codes: missing tftp file 68, missing http file with `-f` 22; tftp `-T` refused 69, http PUT 401 (22 with `-f`).
- curl-mcp `transfer` on tftp: statusCode 0, body base64 when binary; missing: failure kind `CurlExit`, text `TFTP: File Not Found` + newline + `curl exit code 68 (TftpNotFound)`. `curl` tool `-T up.txt tftp://...` exitCode 69.
- Curl.Api: `CurlRequest.Tftp = new CurlTftpOptions { BlockSize = 1024 }`; missing file throws `CurlException`, `Code` `TftpNotFound` (68); a PUT (bytes or stream body) to tftp:// is sent as a read request, so it answers TftpNotFound 68 and uploads nothing.
- Fixtures are text; a binary image is made by `setup: head -c 1300 /dev/urandom > site/firmware.bin` (needs schema 2) and compared with `cmp a b && echo identical`.

- Curl.Api has Unix sockets (`CurlNetworkOptions`) and the TFTP block size (`CurlTftpOptions`) (Curl session, 2026-11-20).
