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
