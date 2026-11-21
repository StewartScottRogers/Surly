<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-21 (batch 9 of the 1,000-recipe plan)

From writing and verifying batch 9 (61 recipes) of the *Surly* cookbook against Curl `7d167fa10a`, Surl `9ad60d80c`, Windows 11. Only findings not already filed. Each repro is the shape from the recipe named in its `review.md`.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Curl.Api uploads (BL-5594 gap) | `HttpMethod.Put` with a body to `gs://` throws `NotBuiltIn` (`gs:// does not support -X PUT; it sends only GET, or PUT with -T`); the same to `s3://` works, and `curl -T` to `gs://` works |
| 2 | Medium | Curl.Api bodies (widens BL-5611) | A `POST` or `PUT` with a body to `amqp://`, `amqp1://` or `mqtt5://` does a receive or SUBSCRIBE instead of publishing (status 0, nothing queued) |
| 3 | Medium | SigV4 over HTTP/2 (check) | A `--aws-sigv4` request over HTTP/2 is refused by surl, over HTTP/1.1 accepted; possibly the signed `host` header vs `:authority`. Also sent to Surl |
| 4 | Low | Curl.Api PUT | A `PUT` with a `Body` sends `Content-Type: application/x-www-form-urlencoded`; `curl -T` sends none |
| 5 | Low | `curl-mcp` on Windows | `curl -v` lines in `standardError` end in CR CR LF |
| 6 | Low | Digest | `curl.exe` and curl-mcp's `curl` tool format the Digest `Authorization` header differently at the same commit (spacing, cnonce encoding); both are accepted |
| 7 | Low | Curl.Api `PostQuote` | A refused post-quote command (e.g. `DELE` of a missing file) is thrown only after the response body is read |

## Repros

**1.** `new CurlRequest(HttpMethod.Put, "gs://bucket/x") { Body = CurlContent.FromStream(...) }` against a `gs://` listener (as in `gs-sign-with-an-hmac-key-in-csharp`) → `CurlException` 4.

**2.** `new CurlRequest(HttpMethod.Post, "amqp://127.0.0.1:5672/jobs") { Body = ... }` → status 0, queue empty (`EnqueueAsync` works). Same for `amqp1://` and `mqtt5://host/topic`.

**3.** See the Surl report's item 1: `curl --cacert server.pem --aws-sigv4 aws:amz:us-east-1:s3 -u K:S https://127.0.0.1:8443/bucket/x` → 401; with `--http1.1` → 200.

**4.** `surl -v --allow-uploads ...`, Curl.Api `PUT` with a stream body; surl's log shows the content type. `curl -T f` shows none.

**5.** curl-mcp `curl` tool with `-v`; `standardError.content` lines end `\r\r\n`.

**6.** `curl --digest -u a:b -v http://...` and the same through curl-mcp: compare the `Authorization:` lines.

**7.** Curl.Api `Ftp = new CurlFtpOptions { PostQuote = { "DELE missing.txt" } }` on a download: the exception arrives after the body.
