<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# coap facts
Checked against: surl 1.0.0 (Surl 763754785), curl (Curl b9ca725077), 2026-11-19, windows.

## Listen URLs and readiness
`coap://`, `coaps://` (DTLS 1.2); UDP only, `ready: none`. Start the main server last and give the first case `curl --retry 5 --retry-all-errors --retry-delay 1`: on Windows, curl to an unbound UDP port exits 7 at once; on Linux and macOS it keeps retransmitting (over 60 s in CI), so a nothing-listening case is `platforms: [windows]` (`platforms.md`).

## Server options that matter
- `--directory site`: files are resources.
- `--user alice:secret`: with any account, plain `coap` reads are 4.01 (NoSec carries no login; `-u` changes nothing). On `coaps` an account is a pre-shared key.
- `--allow-anonymous` (loosens, warns): serves reads despite the account.
- `--self-signed` or `--cert`/`--key` for coaps. None of these and no account: exit 58, `surl: (58) coaps://127.0.0.1:0/ needs a certificate or an account: give --cert <file>, --self-signed for a throwaway one, or --user <name:key> for a pre-shared key` (wording new in 763754785).

## Secure by default
An account on plain coap: `CoAP/1.0 4.01`, curl exit 0 (22 with `-f`).

## Client: curl
- Confirmable GET, payload to stdout. `-i` prints `CoAP/1.0 2.05`, a blank line, the payload. 4.04: exit 0, nothing written; with `-f`, exit 22, `The requested URL returned error: 4.04`.
- coaps: certificate mode only, no pre-shared key (`-u` against a PSK-only surl exits 35, re-probed on b9ca725077). `-k` accepts a throwaway certificate; without it, exit 60. `--cacert` also works.

## MCP
- surl-mcp `start_server` for coap/coaps with options in `arguments`; coaps with neither cert nor account: tool error 58 `CertificateProblem`, text as the console.
- curl-mcp `transfer` (`caFile`, `timeoutSeconds`) answers statusCode 0 and the payload; a 4.01 is statusCode 0, empty body, failure null (indistinguishable from an empty resource). The `curl` tool with `-i` shows `CoAP/1.0 2.05` (CRLF in curl-mcp's output).

## coaps with certificates and accounts (batch 11, windows)
- `--cert`/`--key` and no account: reads 2.05 over coaps with `curl --cacert server.pem` (no `--ssl-no-revoke` needed on Windows); without `--cacert` exit 60; a name the certificate lacks (`localhost`) exit 60.
- Cert AND an account: coaps reads from a certificate client are 4.01 too (no PSK, so no login); `-u` does not help. Account and no cert: PSK only, curl exit 35 `DTLS handshake failed: the server sent the fatal alert HandshakeFailure`, at once.
- Plain coap to a coaps port: no answer; `-m 5` gives exit 28.

## C#
- Curl.Api 0.2.0 CoAP works: StatusCode 0, response code is `Headers.First().Key` (`CoAP/1.0 2.05`, value empty), body the payload; 4.01/4.04 are ordinary responses with empty bodies. `CurlClientOptions.Timeout` gives `OperationTimedOut` (28); PSK-only server `SslConnectError` (35); untrusted `PeerFailedVerification` (60). Surl.Api hosts coap/coaps (`BoundUrls` one per listen URL); no cert and no account: SurlServerStartException 58.

## Gotchas
- `%{response_code}` is 000 after CoAP; use `-i` or `-f`. `-I` sends a GET. Plain coap to a coaps port hangs over 20 s.

## Recipes
coap-get-a-resource, coaps-serve-over-dtls (and twins)

## Observe (coap-watch-a-resource-change and twins; Surl v0.1.5 d64faa4bb / Curl 2145a49eae, 2026-11-24, windows)
- `curl --coap-observe -m N coap://h:p/f` registers (GET Observe 0) and writes the registration answer and each notification as payload + `\n` (`-i`: a `CoAP/1.0 2.05` line first); `-o f` grows as they come. It ends by itself (exit 0) on a notification without Observe (a DELETE gives a 4.04 with empty payload: an empty line), else at `-m` (exit 28, then a GET with Observe 1 deregisters). Observing a missing file: plain 4.04 at once, exit 0. Not in `curl --help all`.
- surl notifies at once ONLY for a CoAP PUT/POST/DELETE. An HTTP PUT, surl-mcp `put_file`, `Files.PutAsync` or an on-disk change is not notified at once; it reaches the observer when the last notification's Max-Age (60 s) lapses (seen at ~62 s). By design: Surl ADR-0097 decision 5. `--aihelp coap`'s "whichever protocol changed it" means eventually.
- Notifications are CON with Observe 1 (registration answer), 2, 3, ..., ETag, Max-Age 60. A CoAP PUT needs `--allow-uploads` (else 4.05) and `--allow-anonymous` (else 4.01; reads still served with no account).
- Curl.Api has no observe option (`CurlRequest` extension settings are DNS only): the C# twin observes over a raw `UdpClient` (CON GET, token, Observe option 0x60, Uri-Path 0x5D 0x01 + 14-byte name; ACK each CON notification with `60 00 mid`). Curl.Api CoAP `PUT`/`DELETE` work: `Headers.First().Key` is `CoAP/1.0 2.04` / `2.02` / `4.05` / `4.01`.
- Agent: run the observing `curl` call as a background task; the registration is done once `get_exchanges(scheme: "coap")` shows a `sent` chunk for it. One `ready: none` console server per UDP port; `ready: port` would wait on UDP ports and fail.
- Do not reuse another writer's scratch folder or ports near 47xxx: they are shared on this machine.
