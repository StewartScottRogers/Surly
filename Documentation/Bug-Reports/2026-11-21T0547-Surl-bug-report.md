<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-21 (batch 9 of the 1,000-recipe plan)

From writing and verifying batch 9 (61 recipes: moving clients between protocols, AMQP and Kafka, Unix sockets, connection reuse, GCS, plain-HTTP S3, WebSocket frames) of the *Surly* cookbook against Surl `9ad60d80c`, Curl `7d167fa10a`, Windows 11. Only findings not already filed; some may be fixed on master. Each repro is the command shape from the recipe named in its `review.md`.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | S3 SigV4 over HTTP/2 | A correctly signed `curl --aws-sigv4` request over HTTP/2 is refused (401, or 403 with `--auth aws-sigv4`); over HTTP/1.1 the same request gets 200. Possibly `Host` vs `:authority` in the signed headers; it may be curl's signing, so the same item goes to Curl |
| 2 | Low | `get_exchanges` | Only the first 64 KiB of an exchange is returned, with no marker that it was cut |
| 3 | Low | Events | `exchange_bytes` events come back empty, and `get_exchanges` chunks carry no timestamps |
| 4 | Low | GCS events | A refused HMAC put with uploads off produces no `login_judged` event, while a token put does |
| 5 | Low | Logging | With `-v`, an S3 put logs no "stored" line; an HTTP PUT logs `PUT /x: 201, N bytes stored at ...` |
| 6 | Low | Unix sockets | `--unix-socket` with a path over the platform limit fails `(45) ... Bind failed`; the message should say the path is too long. `SurlServerOptions` has no Unix-socket property (`AdditionalArguments` works) |

## Repros

**1.** `surl --directory store --cert server.pem --key server.key --user AKIAIOSFODNN7EXAMPLE:s3cret --allow-uploads s3://127.0.0.1:9000/ https://127.0.0.1:8443/` (as in `http-move-uploads-to-s3`), then `curl --cacert server.pem --aws-sigv4 aws:amz:us-east-1:s3 -u AKIAIOSFODNN7EXAMPLE:s3cret https://127.0.0.1:8443/bucket/x` (HTTP/2 by default) → 401; with `--auth aws-sigv4` on the server, 403. Add `--http1.1` → 200. Expected: 200 both ways.

**2.** surl-mcp `start_server` (http), put a 200 KiB file, fetch it, `get_exchanges`. Observed: 65,536 bytes of the exchange. Expected: all, or a `truncated` flag and the full length.

**3.** surl-mcp `get_events` after a fetch: `exchange_bytes` events have empty content; `get_exchanges` chunks have no time field.

**4.** a `gs://` listener (`surl --cert ... --key ... --user GOOGTESTKEY:s3cret gs://127.0.0.1:PORT/`) without `--allow-uploads`: an HMAC-signed `curl -X PUT` is refused, and `get_events` shows no `login_judged`; a bearer-token put does show one.

**5.** `surl -v ... s3://127.0.0.1:PORT/` and an S3 put to it: no stored line. `surl -v --allow-uploads http://...` and `curl -T f http://.../f`: `PUT /f: 201, N bytes stored at ...`.

**6.** `surl --unix-socket /tmp/<a 120-character name> --directory site http://localhost/` → `surl: (45) ... Bind failed`. Expected: "the socket path is longer than the platform allows (N bytes)".
