<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# tls facts
Checked against: surl 1.0.0 (Surl 9ad60d80c), curl 8.22.0 (Curl 7d167fa10a), 2026-11-20, windows (P12 messages re-probed; the rest carried from Surl 763754785 / Curl b9ca725077).
Covers the TLS options common to every secure scheme; protocol sheets cover the rest.

## Listen URLs and readiness
Any TLS scheme (`https`, `mqtts`, `ftps`, `wss`, ...) needs a certificate; `ready: port` (TCP).
`surl --aihelp tls` is the full list.

## Server options that matter
- `--cert <file> [--key <file>] [--pass <phrase>]`: PEM by default; a PEM `--cert` with no key in it needs `--key`.
- `--cert-type P12` (also `DER`; read in any case) with `--pass`: a PFX / PKCS #12 file holding cert and key. `--key` beside a P12 `--cert` exits 2 (`option --key: is badly used here`); `--pass` with no `--cert` exits 2.
- `--tlsv1.3` lowest version 1.3; `--tlsv1.2` is the default; `--tlsv1.0`/`--tlsv1.1` loosen. `--tls-max <1.0..1.3>` highest. Lowest above highest (`--tlsv1.3 --tls-max 1.2`) exits 2: `surl: option --tls-max: is badly used here`.
- `--self-signed` (loosens, warns): throwaway cert; curl needs `-k`. Cannot be combined with `--cert` (exit 2).

## Secure by default
- No `--cert` and no `--self-signed`: `surl: (58) <url> needs a certificate: give --cert <file>, or --self-signed for a throwaway one`, exit 58.
- P12, all exit 58, `Could not load the server certificate: ` then: wrong `--pass` `'server.pfx' is a P12 file the --pass given does not open.`; no `--pass` `'server.pfx' is a P12 file that needs --pass.`; a file that is not P12 (a PEM, a truncated PFX) `'<file>' is not a P12 certificate Surl can read.`; a PFX given `--cert-type DER` `'server.pfx' is not a Der certificate Surl can read.` (Surl 9ad60d80c, BL-1445). PFX without `--cert-type P12`: exit 58, `'server.pfx' holds no PEM certificate.` Missing file: exit 58, `Cannot read the --cert file '<file>'.`
- A handshake that fails on version is a log line (`TLS handshake failed: ...`), the server keeps serving.

## Client: curl
- `--cacert server.pem` trusts a self-signed certificate (it is its own authority); without it curl exits 60. A name the certificate lacks (`localhost` vs `IP:127.0.0.1`) also exits 60.
- `--tls-max 1.2` against a `--tlsv1.3` server, and `--tlsv1.3` against `--tls-max 1.2`: exit 35, stdout empty. The wording (`Recv failure: Connection was reset` or `SEC_E_ILLEGAL_MESSAGE`) is the TLS library's (schannel here): match `curl: (35)` only.
- `curl` with no version option works against a `--tlsv1.3` server (it offers 1.3), on Windows and Linux.
- macOS: surl serves TLS 1.2 at most (SslStream; Surl ADR-0010); any TLS 1.3 handshake is exit 35. See `platforms.md`.
- `-w '%{ssl_version}'` is not a variable in this curl.
- `--proto =https` with an `http://` URL: exit 1, `curl: (1) Protocol "http" is disabled`, nothing sent. Curl.Api `CurlNetworkOptions.Protocols = "https"`: `CurlException` `UnsupportedProtocol`. curl-mcp: `curl` tool only (`transfer` has no protocols field).
- One surl serves `http://` and `https://` listen URLs side by side with one `--cert`/`--key`; `--trace-ascii` names each exchange's scheme (`opened: https from`).
- Plain HTTP to an https port: Windows curl exit 56 (`Recv failure: Connection was reset`); not probed on Linux/macOS. https to a plain port: exit 35.

## MCP
- `surl-mcp start_server`: no TLS fields; pass options in `arguments` (`["--cert","server.pem","--key","server.key","--tlsv1.3"]`); a refusal is a tool error with structured `exit_code` (58) and `exit_code_name` (`CertificateProblem`), text is surl's line. Certificate files must be under `SURL_MCP_ROOT`.
- `curl-mcp transfer` has `insecure`, `caFile` (`--cacert`; must lie under `CURL_MCP_ROOT`, else PathRefused) and `minTlsVersion` (`1.0`..`1.3`, as `--tlsv1.x`) since Curl adadcb5959; answer `statusCode`, `body.content`. No `--tls-max`: use the `curl` tool (`args` list) for that and `-u`.
- `curl` tool answer: `exitCode`, `standardOutput.content`, `standardError.content` (null when empty).

## C#
- `SurlServerOptions`: `CertificateFile`, `KeyFile`, `KeyPassphrase`, `SelfSigned`; no properties for `--cert-type` or the TLS range: use `AdditionalArguments` (an `IList<string>`, so `Add` each word; no `AddRange`). A refused start throws `SurlServerStartException`, `Message` is surl's text (two lines for exit 2).
- `Curl.Api`: `CurlClient.Create(new CurlClientOptions { Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { CertificateAuthorityFile, SslVersion, SslVersionMaximum, VerifyPeer } } })`; enums `SslVersion.{Default,Tls12,Tls13}`, `SslVersionMaximum.{None,Tls12,Tls13}` in `Curl.Api.Options.Tls`. A failed handshake throws `CurlException` with `Code` `SslConnectError` (35).
- Relative cert paths are read from the program's working directory (the case root).

## Gotchas
- openssl under Git Bash rewrites `/CN=...` as a path and fails silently: use `MSYS_NO_PATHCONV=1` by hand; `setup:` is fine.
- Servers started together in one case root need their own `--directory`, or the second dies on the lock (exit 37/124).
- A PFX made by OpenSSL 3 with its default encryption loads in surl on Windows.
- The bibliography has no PKCS #12 (RFC 7292) or TLS 1.0/1.1 deprecation (RFC 8996) entry.

## Recipes
https-refuse-clients-below-tls-1-3 (and -in-csharp, -from-an-agent), http-move-a-client-to-https (and twins), https-serve-a-pfx-certificate (and twins), mqtts-log-in-over-tls (and twins), https-serve-a-certificate-chain (and twins).

- Curl.Api: `VerifyHost = false` skips only the name check. A self-signed peer still fails 60; use `VerifyPeer = false` as well (`curl -k` is both) or trust the certificate with `CertificateAuthorityBlob` (Curl session, 2026-11-20).

## Certificate chains (https-serve-a-certificate-chain, 2026-11-20, windows)
- `--cert chain.pem --key leaf.key` with the leaf first, then the intermediate (`cat leaf.pem inter.pem`): surl sends both; a client with `--cacert root.pem` alone verifies (exit 0). A root appended (`full.pem`) works too. A leaf-only file with a client that trusts only the root: exit 60. No `--cacert`: 60.
- The key is matched with the FIRST certificate: intermediate first (`cat inter.pem leaf.pem`), or the intermediate's key beside the chain: exit 58 `surl: (58) Could not load the server certificate: The private key cannot be read or does not match the certificate.` (wording is Windows; match the prefix).
- Windows (Schannel): any real chain needs `--ssl-no-revoke` (Curl.Api `SslOptions = SslOptions.NoRevoke` in `CurlTlsPeerOptions`), else exit 60 `schannel: the revocation status is unknown` (test certs have no CRL or OCSP address). A single self-signed certificate used as the trust anchor needs no such option. See `platforms.md`.
- Windows caches intermediates it has seen: after a server sent `chain.pem`, a leaf-only server of the SAME intermediate verified with `--cacert root.pem` (exit 0). The leaf-only case therefore uses a second intermediate (`inter2`) signed by the same root and a `leaf2` nobody sent in a chain. Not probed on Linux/macOS.
- openssl setup for a chain: root `req -x509 ... -addext basicConstraints=critical,CA:TRUE -addext keyUsage=critical,keyCertSign,cRLSign`; intermediate by `x509 -req -CA root.pem -CAkey root.key -CAcreateserial -extfile inter.ext` (`basicConstraints=critical,CA:TRUE,pathlen:0`, `keyUsage=critical,keyCertSign,cRLSign`); leaf `subjectAltName=IP:127.0.0.1`, `basicConstraints=CA:FALSE`. `printf "...\n" > file` works in `setup:`.
- Trusting the intermediate alone (`--cacert inter.pem`) also verifies on Windows (partial chain); not claimed.
- Curl.Api: `CertificateAuthorityBlob` is `ReadOnlyMemory<byte>?` and init-only: build it in the initializer (`ReadOnlyMemory<byte>? root = File.ReadAllBytes(...)`). Failure: `CurlException` `Code` `PeerFailedVerification` (60). Start refusal: `SurlServerStartException` `ExitCode` `CertificateProblem` (58), `Message` `(58) Could not load the server certificate: ...`. An https fetch answers a `ReasonPhrase` of "" (print the code only).
- surl-mcp start refusal: structured `exit_code` 58, `exit_code_name` `CertificateProblem`; curl-mcp `curl` tool nonzero exit (60) is a tool error; `transfer` cannot skip revocation checking.

- Do not use Curl.Api `UseSsl.All` (or Try/Control) in a recipe until Curl BL-5640 lands: on `ftp://` it sends USER and PASS in clear with no AUTH TLS, and on `imap://` the STARTTLS login fails 67. Use the implicit-TLS scheme (`ftps://`, `imaps://`, `pop3s://`, `smtps://`) instead (Curl session, 2026-11-20).
