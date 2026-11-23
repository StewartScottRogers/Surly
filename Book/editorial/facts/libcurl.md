<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# libcurl facts (the Libcurl package: C programs ported to C#)
Checked against: Libcurl 0.2.0 (Curl 7d167fa10a), surl 1.0.0 (Surl 9ad60d80c), 2026-11-21, windows.

## Shape
- `#:package Libcurl@0.2.0` alone; the program is a client of a `surl` process (`servers:` block), as `http-port-a-c-program-with-libcurl`. Usings: `Curl.Api.Callbacks` (WriteCallback), `Curl.Api.Codes` (CurlCode, CurlUrlCode, CurlHeaderCode), `Libcurl.Easy`, `Libcurl.Global`, `Libcurl.Info`, `Libcurl.Misc.ErrorText`, `Libcurl.Options`, plus `Libcurl.Url` / `Curl.Api.EngineContracts.Url` (CurlUrlPart, CurlUrlFlags), `Libcurl.Headers` / `Curl.Api.EngineContracts.Headers` (CurlHeader, CurlHeaderOrigin), `Libcurl.Mime`.
- Handles come from the `CurlLibrary` (`CurlGlobal.Init`): `EasyInit`, `UrlInit` (`curl_url()`), `MimeInit` (`curl_mime_init` takes no easy handle here), `MultiInit`, `ShareInit`. Dispose = cleanup.
- Names: `CURLOPT_X_Y` is `CurlOption.XY` with one capital per word as upstream joins them (`Writefunction`, `Mimepost`, `Curlu`, `Headerfunction`); `CURLINFO_*` likewise (`ConnectTimeT`, `StarttransferTimeT`, `SizeUploadT`, `NumConnects`). `curl.GetInfo(CurlInfo.X, out long v)` (generic) or `out double` for the old seconds timers.
- Reflection on the package (members, enum values) is the quickest lookup: a file-based app `Assembly.GetType(...)` dump; the XML docs are `lib/net10.0/Libcurl.xml` in the nupkg.

## URL API (`CurlUrlHandle`)
- `Set(part, value, flags)`, `Get(part, flags, out string?)`, static `ErrorText(code)` = `curl_url_strerror`. Codes as upstream: no port 15 `No port number present`, no query 16 `No query present`, `:99999` and `eighty` 4 `Port number was not a decimal number between 0 and 65535`, `htp://` 5 `Unsupported URL scheme`, no scheme 27 `Bad scheme`, raw space 3 `Malformed input to a URL function`.
- `CURLU_URLENCODE` on PATH: space -> %20. QUERY with `AppendQuery | UrlEncode`: joined by `&`, first `=` kept, space -> `+` (`region=north+east&lang=en`). Parse lowercases the scheme, keeps `:80`, removes dot segments. `DefaultPort` gives 80. Setting URL on a handle with a URL resolves it relative (`../`, `?q`, `//host`).
- `SetOption(CurlOption.Curlu, h)` fetches the handle's URL; surl decodes %20 to the file with the space.

## Header API (`CurlEasyHeaders`, static)
- `Header(easy, name, index, CurlHeaderOrigin.Header, -1, out CurlHeader?)` answers `CurlHeaderCode`: before any transfer 3 NoHeaders; missing 2; index past amount 1 BadIndex; request 5 on a one-request handle 4 NoRequest. Name case-insensitive; `CurlHeader` record {Name (server's spelling), Value, Amount, Index, Origin}.
- `NextHeader(easy, origin, -1, prev)` (prev null to start) walks in received order: surl 200 `Date, Server, Last-Modified, Content-Type, Content-Length`; 404 `Date, Server, Content-Length: 0`.

## Mime (`CurlMime`, `CurlMimePart`)
- `AddPart()`, part `SetName`, `SetData(string)` (CURL_ZERO_TERMINATED), `SetFileData(path)` (file name from the path; unreadable file: `CurlCode` 26 ReadError at once, text `Failed to open/read local data from file/application`), `SetType`. `SetOption(CurlOption.Mimepost, mime)`: POST multipart/form-data; libcurl sends no User-Agent.
- surl serves no POST: with `--allow-anonymous` it is 405 after reading the body; `--trace-ascii trace.txt` records the whole body (lines cut at 64 bytes, line ends in a body shown as `.`). Two parts (title=November, file nov.csv 22 bytes, text/csv): Content-Length 325, `SizeUploadT` 325. Boundary random (`-` x24 + 22 chars).

## Timers
- `ConnectTimeT`, `PretransferTimeT`, `StarttransferTimeT`, `TotalTimeT` are microseconds (long); `StarttransferTime` the same as double seconds. No 4 s first-request cost appears in the timers (cold first transfer: connect ~0.06-0.16 s).
- With `--fault "delay;stage=command;path=/slow.txt;ms=1500"`: starttransfer >= 1.5 s, pretransfer < 0.2 s. Second URL on one easy handle reuses the connection: `NumConnects` 0, `ConnectTimeT` 0.

## Recipes
http-port-a-c-program-with-libcurl, ws-echo-messages-with-libcurl, rtsp-play-a-stream-with-libcurl,
http-build-urls-with-the-libcurl-url-api, http-post-a-form-with-curl-mime, http-read-headers-with-curl-easy-header, http-take-timings-with-curl-easy-getinfo.

## Progress callback, multi handle, read callbacks, SFTP pin, SMTP (batch 13: ftp-abort-from-a-progress-callback, http-run-many-transfers-with-a-multi-handle, sftp-upload-with-a-read-callback, smtp-send-mail-from-a-libcurl-port; Libcurl 0.2.0, Surl v0.1.5 (d64faa4bb) / Curl 2145a49eae, 2026-11-22, windows)
- Delegates live in `Curl.Api.Callbacks`: `TransferInfoCallback(long dltotal, long dlnow, long ultotal, long ulnow) -> int` (CURLOPT_XFERINFOFUNCTION), `ProgressCallback` (doubles, CURLOPT_PROGRESSFUNCTION), `ReadCallback(Span<byte>) -> long` (CURLOPT_READFUNCTION; 0 ends), `WriteCallback(ReadOnlySpan<byte>) -> long`. No *DATA needed: closures. Package `#:package Surly.Libcurl@*`.
- Xferinfo: called only with `CurlOption.Noprogress` 0L (default 1: never called). Non-zero return: `CurlCode.AbortedByCallback` 42, text `Operation was aborted by an application callback`.
- FINDING (Curl): the xferinfo callback is NOT called while the transfer waits on the server (FTP RETR held 3-4 s by `--fault "delay;stage=command;command=RETR;path=/slow.bin;ms=3000"`: one call at start, then only when data arrives, first at dlnow 102400 of a 1,000,000-byte file); upstream calls it about once a second while idle. And `GetInfo(CurlInfo.TotalTimeT)` read INSIDE the callback answers 0 throughout (upstream: time so far). Recipe uses its own Stopwatch and states the difference.
- Multi: `library.MultiInit()` -> `CurlMulti` (await using); `AddHandle`, `PerformAsync(ct)` -> `(CurlMultiCode, int running)`, `PollAsync(Memory<CurlWaitSocket>.Empty, 1000, ct)` -> `(CurlMultiCode, int numfds)` (`using Curl.Api.EngineContracts.Multi;`), `InfoRead(out int left)` -> `CurlMessage?` {`Type` `CurlMultiMessageType.Done`, `Easy`, `Result`}, `RemoveHandle`, `CurlErrorText.Multi`. Three 3 s-held HTTP paths + one fast: all done in ~3.3 s, fast one reported first; a 404 is Done with `Ok`; port 1 is `CouldntConnect` 7 for that handle only. `CurlInfo.EffectiveUrl` with `out string?`.
- SFTP upload: `Userpwd`, `SshHostPublicKeySha256` (base64 from `ssh-keygen -l`, no `SHA256:`), `Upload` 1L, `Readfunction`, `InfilesizeLarge`; 300,000 bytes take more than one read; `SizeUploadT` 300000. Wrong key 60 `SSL peer certificate or SSH remote key was not OK`; wrong password 67 `Login denied`; no `--allow-uploads` 9 `Access denied to remote resource`.
- SMTP: `CurlSlist.Append(null, rcpt)` / `CurlSlist.FreeAll` (`Libcurl.Misc.DatesAndLists`), `MailFrom` "<a>", `MailRcpt` slist, `Readfunction` one line a call, `Upload` 1L; CRAM-MD5 used with no option; 126-byte message in 5 reads, `ResponseCode` 250; stored with `Received: from <EHLO=url path> ([127.0.0.1]) by surl with ESMTPA; <date>`. Recipient outside `--mail-domain`: 55 `Failed sending data to the peer`; wrong password 67.
