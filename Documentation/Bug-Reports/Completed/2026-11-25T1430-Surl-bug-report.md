<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-25 (batch 15 of the 1,000-recipe plan)

From writing and verifying batch 15 (37 recipes) of the *Surly* cookbook against Surl v0.1.8 `9f8005dff`, Curl `2145a49eae`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | High (security) | SOCKS5 proxy log | With `-v`, the SOCKS5 proxy's log lines show passwords that pass through it in clear: an FTP `PASS` and an HTTP Basic `Authorization` value. The origin's own lines for the same exchanges show `****` |
| 2 | Medium | S3 SigV4 | A signature whose credential scope names another region (`eu-west-1`) is accepted on a `us-east-1` endpoint, 200. AWS refuses it (`AuthorizationHeaderMalformed`, the region is wrong) |
| 3 | Low | HTTP override | A `ProtocolError("401")` response override answers 401 with no `WWW-Authenticate` header. RFC 9110 section 11.6.1 requires a 401 to send at least one challenge; the server's own 401 offers Digest, Basic and Bearer |
| 4 | Low | S3 errors | The `SignatureDoesNotMatch` error body has no `CanonicalRequest` or `StringToSign`, which AWS includes so a client author can see where the signatures differ |
| 5 | Question | Bus, `--topic-map` | With `--topic-map nats:orders.created=mqtt:shop/orders/new`, a message published to MQTT `orders/created` reaches no NATS subscriber, not even one to `>`. That topic's default NATS name, `orders.created`, is the map's alias for another topic, so dropping it may be deliberate; ADR-0102 3.3 does not say. Intended? |

Re-run on 2026-11-25 against v0.1.8: 1 and 2. Items 3 to 5 were seen the same day by the recipe writers on the same build and not re-run separately. Not reported, because Surl documents them: an accept-stage fault rule spends one of its `times` on a connection that sends nothing (`surl --aihelp testing`), and a fault 503 now has a 24-byte text body.

## Repros

**1.** `surl --directory site --user app:s3cret-pw --allow-plaintext-auth http://localhost:18080/ ftp://localhost:18021/ socks5h://127.0.0.1:18108/ -v --log-file socks.log` (`site/hello.txt` present).
- `curl -u app:s3cret-pw -x socks5h://127.0.0.1:18108 ftp://localhost:18021/hello.txt`
- `curl --basic -u app:s3cret-pw -x socks5h://127.0.0.1:18108 http://localhost:18080/hello.txt`
- `socks.log` shows the proxy's exchange `#1 < PASS s3cret-pw\r\n` and `#4 < Authorization: Basic YXBwOnMzY3JldC1wdw==\r\n`. The origin's exchanges show `#2 < PASS ****\r\n` and `#5 < Authorization: Basic ****\r\n`.
- Expected: `****` on the proxy's lines too.

**2.** `surl --directory store --cert server.pem --key server.key --user K:S s3://127.0.0.1:18443/` (`store/photos/hello.txt` present; the certificate names `*.s3.us-east-1.amazonaws.com`).
- `curl --cacert server.pem --connect-to ::127.0.0.1:18443 --aws-sigv4 aws:amz:eu-west-1:s3 -u K:S https://photos.s3.us-east-1.amazonaws.com/hello.txt` gives 200, as `aws:amz:us-east-1:s3` does.
- Expected: 400 `AuthorizationHeaderMalformed`, as AWS answers a scope whose region is not the endpoint's.

**3.** Surl.Api, an `https://` server with accounts and an override that returns `SurlFault.ProtocolError("401")` for a path (recipe `http-expire-tokens-with-a-response-override`, case `answer`).
- `curl -D - https://127.0.0.1:P/<path>` shows `HTTP/1.1 401` with no `WWW-Authenticate` line.
- Expected: at least one challenge, such as the server's own `Bearer realm=...`.

**4.** Same server as 2. `curl ... --aws-sigv4 aws:amz:us-east-1:s3 -u K:wrong https://photos.s3.us-east-1.amazonaws.com/hello.txt` gives 403 with `<Code>SignatureDoesNotMatch</Code>`, but no `<CanonicalRequest>` or `<StringToSign>` elements.
- Expected: both, as AWS's error has them.

**5.** `surl --allow-anonymous --topic-map nats:orders.created=mqtt:shop/orders/new nats://127.0.0.1:P1/ mqtt://127.0.0.1:P2/`, a NATS subscriber to `>`, then `curl -d B mqtt://127.0.0.1:P2/orders/created`.
- Observed: the NATS subscriber receives nothing.
- Recipe `nats-join-two-names-with-a-topic-map` claims only the mapped direction.
