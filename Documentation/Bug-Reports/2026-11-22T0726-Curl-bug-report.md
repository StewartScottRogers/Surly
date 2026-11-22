<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-22 (batch 10 of the 1,000-recipe plan)

From writing and verifying batch 10 (60 recipes) of the *Surly* cookbook against Curl `c84035f797`, Surl `5da5b3eaf`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Curl.Api MQTT 5 (BL-5681) | A `POST` with a body to `mqtt5://host/topic` still sends a SUBSCRIBE and publishes nothing; BL-5681's fix ("publish a Curl.Api body to amqp, amqp1, mqtt5, ...") is in this build |
| 2 | Medium | Curl.Api gRPC streams | A server stream that ends in a non-OK status (13, or a deadline) throws `CurlException` 22 and the replies received before it are lost; the console and curl-mcp keep them in stdout |
| 3 | Low | Curl.Api headers | `Headers = { new("grpc-timeout", "") }` sends the header with an empty value (the server answers INTERNAL `malformed grpc-timeout`); `curl -H "grpc-timeout:"` removes it. `RemovedHeaders` works |
| 4 | Low | TELNET (Windows) | `curl telnet://host:port` reading standard input (without `-T -`) sends each CR LF as LF; upstream curl sends the bytes unchanged |
| 5 | Check | TLS (Windows) | Against `natss://`, `--cacert server.pem` alone fails 35; `--ssl-no-revoke` fixes it. The same certificate over `mqtts://` and `xmpps://` needs no `--ssl-no-revoke`. Is the revocation check applied differently by scheme? |

## Repros

**1.** `new CurlRequest(HttpMethod.Post, "mqtt5://127.0.0.1:1883/sensors/t") { Body = CurlContent.FromString("21.5") }` against `surl mqtt://127.0.0.1:1883/`: surl's log shows a SUBSCRIBE, nothing published. Recipe `mqtt5-subscribe-over-websocket-in-csharp` publishes through `mqtt://` instead.

**2.** `surl grpc://127.0.0.1:50051/` and a Curl.Api call to `surl.test.Echo/Expand` with `{"message":"hi","count":5,"failAfter":3}` (or a deadline): `CurlException` 22, no replies. `curl --grpc-proto echo.proto -d ... grpc://.../surl.test.Echo/Expand` prints the three replies, then exits 22.

**3.** As in the table; recipe `grpc-time-out-a-slow-call-in-csharp`.

**4.** `printf 'PING\r\n' | curl telnet://127.0.0.1:4222` against surl's NATS listener: surl answers `-ERR 'Parser Error'` (it got `PING\n`).

**5.** `surl --cert server.pem --key server.key --user a:b natss://127.0.0.1:4443/`, `curl --cacert server.pem -u a:b -d hi natss://127.0.0.1:4443/x` → 35; add `--ssl-no-revoke` → 0. Same certificate, `mqtts://` → 0 without it.
