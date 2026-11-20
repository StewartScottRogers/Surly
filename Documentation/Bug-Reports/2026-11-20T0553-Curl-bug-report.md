<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-20 (batch 3 of the 1,000-recipe plan)

From writing and verifying batch 3 (62 recipes) of the *Surly* cookbook. Follows `2026-11-19T2203-Curl-bug-report.md`; earlier items still stand. Items 1 to 3 widen that report's #1: **Curl.Api can upload only over HTTP and SMTP (stream body).**

**Build tested:** the Curl port at `b9ca725077`: `curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`. Windows 11. Server: Surl `763754785`.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | **High** | Curl.Api S3/Azure | `HttpMethod.Put` with a body to `s3://` or `az://` throws `CurlException` 4 `NotBuiltIn`: `s3:// does not support -X PUT; it sends only GET, or PUT with -T`. Curl.Api has no way to say `-T` |
| 2 | **High** | Curl.Api TFTP | A `PUT` to `tftp://` is sent as a read request (exit 68, not an upload) |
| 3 | Medium | Curl.Api TELNET | A `Body` (bytes or stream; GET, PUT or POST) is ignored: the session opens, the banner arrives, no line is sent |
| 4 | Medium | S3 / Azure URLs | `curl` drops the query string on `s3://` and `az://`, so `?prefix=` cannot be used; the prefix must go in the path |
| 5 | Check | Retry wording | The port prints `Retrying in 1 second.`; please compare with upstream curl's wording |
| 6 | Check | Curl.Api TLS | With `Surl.Api`'s `SelfSigned` certificate, a virtual-host S3 URL (`bucket.s3.us-east-1.amazonaws.com`) fails 60 even with `VerifyHost = false`. Possibly a missing `VerifyPeer = false` in the recipe draft; please confirm whether 60 should follow from the host name |
