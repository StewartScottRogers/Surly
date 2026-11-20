<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# tls facts
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19, windows.
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
- P12 with a wrong or missing `--pass`: exit 58, `Could not load the server certificate: 'server.pfx' is not a P12 certificate Surl can read.` (does not name the passphrase). PFX without `--cert-type P12`: exit 58, `'server.pfx' holds no PEM certificate.` Missing file: exit 58, `Cannot read the --cert file '<file>'.` Some wrong-type combinations (a PEM given `--cert-type P12`, a PFX given `--cert-type DER`) also say `Cannot read the --cert file`.
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
- `curl-mcp transfer` has only `insecure`: no `--cacert`, no TLS version. Use the `curl` tool (`args` list) for `--cacert`, `--tls-max`, `--tlsv1.3`, `-u`.
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
https-refuse-clients-below-tls-1-3 (and -in-csharp, -from-an-agent), http-move-a-client-to-https (and twins), https-serve-a-pfx-certificate (and twins), mqtts-log-in-over-tls (and twins).
