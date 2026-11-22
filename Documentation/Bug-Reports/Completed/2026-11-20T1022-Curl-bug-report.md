<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-20 (batch 4 of the 1,000-recipe plan)

From writing and verifying batch 4 (60 recipes: CI scenarios, mail scenarios, Kafka, XMPP, gRPC, SignalR, DNS, Redis, TFTP, WebSocket, DICT, Gopher, HTTP) of the *Surly* cookbook. Only findings not already filed (BL-5594 to BL-5608) are listed.

**Build tested:** the Curl port at `b9ca725077`: `curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`. Windows 11. Server: Surl `763754785`.

**Widening BL-5594 (Curl.Api request bodies):** a body reaches the server only over HTTP and SMTP (stream). Newly seen:
- **IMAP APPEND:** `PUT` with `CurlContent.FromStream(stream, length)` fails `CurlException` 25, `Cannot APPEND with unknown input file size`. The stream body sets POSTFIELDSIZE, not INFILESIZE.
- **Kafka produce:** `POST` with a body to `kafka://host/topic` returns status 0 and no record is stored.
- **gRPC:** no request body is sent. `GET` works by reflection with `{}`; `POST` with a body returns `{}`; with a `Content-Type` it fails 415.
- **SignalR:** `SendAsync` to `signalr://` with a body negotiates and handshakes, never sends the invocation, then times out with 0 bytes.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Curl.Api IMAP | `HttpMethod` refuses a value with spaces (`FormatException`), so IMAP `STORE` and `MOVE` cannot be sent as custom requests |
| 2 | Medium | Write-out | After `--max-time` (exit 28) with `--limit-rate 25k`, `-w` prints `size_download` and `speed_download` as 0 though 51200 bytes were written |
| 3 | Low | Curl.Api | `CurlResponse.Info.HttpVersion` is 0 when read right after `SendAsync` |
| 4 | Low | Curl.Api SMTP | A `NOOP` custom request gives `StatusCode` 0; `curl -X NOOP -w %{response_code}` prints 250 |
| 5 | Low | Curl.Api TFTP | `CurlTftpOptions.BlockSize` throws `ArgumentOutOfRangeException` outside 8 to 65464; `curl --tftp-blksize 70000` clamps to 65464. The two should agree |
| 6 | Low | Curl.Api DNS | No property for `--dns-json` |
| 7 | Check | Libcurl | `grpc://` and `kafka://` answer `UnsupportedProtocol`. Intended for the C API surface? |
| 8 | Check | Rate limit | `--limit-rate 25k` on 100000 bytes averaged about 33500 B/s; please compare with upstream curl's overshoot |
