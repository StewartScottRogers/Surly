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

- natss (2026-11-22, windows): `curl --cacert server.pem` alone against `natss://` is exit 35 SEC_E_ILLEGAL_MESSAGE; `--ssl-no-revoke` fixes it (mqtts, mqtt5s and xmpps need no such option). See nats.md.

- rediss, signalrs, socketios, stomps (2026-11-22, windows): `curl --cacert server.pem` alone works against all four (no `--ssl-no-revoke`, unlike natss). All are TLS from the first byte and client-first, so the Curl BL-5750 shape (TLS starting before a plain server greeting) does not arise; not run on Linux or macOS.

## Client certificates (https-require-a-client-certificate, + twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `--cacert ca.pem` on the server (Surl.Api `CaCertificateFile`) is what makes it ask for a client certificate; without it none is asked. Missing file: exit 2 `surl: The file 'missing.pem' provided to --cacert does not exist` (+ `option --cacert: is badly used here`); a file with no certificate: exit 77 `surl: (77) Could not load the CA certificates: The --cacert file 'notes.txt' holds no certificate Surl can read.` (Surl.Api `FailedInit` / `CaCertificateBadFile`; surl-mcp `exit_code` 2 / 77, `lines[0]` without the `surl: ` prefix).
- No client cert, or one from another authority: handshake refused. curl exit varies: Windows 55 `Send failure: Connection was reset` at TLS 1.3, 35 at `--tls-max 1.2`; curl-mcp `curl` tool once gave 56 (SEC_E_ILLEGAL_MESSAGE); Curl.Api SendError/SslConnectError. Claim "fails" only (`|| echo refused`).
- Server log (`--log-file`, no -v needed; Surl.Api `Log`): `#N * TLS handshake failed: The remote certificate was rejected by the provided RemoteCertificateValidationCallback.` for both (.NET text). The ready probe of verify adds an `unexpected EOF` line, not a rejected one. surl-mcp: the refused exchange ends with `received_byte_count` 0 (no handshake-failure event).
- Client side: PEM `--cert client.pem --key client.key` on Windows: exit 58 `curl: (58) schannel: Failed to import cert file client.pem, last error is 0x80092002` (Curl.Api `SslCertProblem`); P12 `--cert client.p12:s3cret --cert-type P12` works (Curl.Api `Certificate`, `CertificateType = "P12"`, `KeyPassword`). Linux/macOS expected to take PEM (OpenSSL build), not yet seen in CI. curl-mcp `transfer` has no client-cert field: use the `curl` tool.
- No certificate store touched: Cert:\CurrentUser\CA, Root, My checked empty of the test names after the runs.

## TLS-SRP (https-log-in-with-tls-srp, + twins; same builds)
- Server: `--tlsuser u --tlspassword p` (implies `--tlsauthtype SRP`) serves TLS 1.2 SRP suites, no cert needed. Refusals exit 2: `option --tlsuser: needs --tlspassword`; `option --tlsauthtype: needs --tlsuser and --tlspassword, or --srp-verifiers and --srp-groups`; `option --tlsuser: cannot be used with --tlsv1.3`; `option --tlsuser: needs a listen URL that uses TLS` (http URL). Malformed `--srp-groups` line: exit 37 `Could not read the --srp-groups file 'tpasswd.conf': line 1: the prime is not a GnuTLS SRP base64 number`. `--self-signed` beside TLS-SRP is accepted (warns).
- NO SRP CLIENT in these builds: curl 8.22.0 port warns `Warning: --tlsauthtype|--tlsuser|--tlspassword is deprecated and has no function anymore` (hidden by `-s`) and fails exit 35 (Curl ADR-0480); Curl.Api `TlsAuthType/TlsAuthUserName/TlsAuthPassword` have no effect (SslConnectError); curl-mcp the same. Server log: `TLS handshake failed: The ClientHello offers no SRP suite the server can serve.` Without -k it is still 35, not 60.
- openssl s_client `-srpuser/-srppass pass:x` on this machine: MSYS2 ucrt64 OpenSSL 3.5.9 segfaults (139); Git for Windows OpenSSL 3.5.7 stalls after ClientKeyExchange (also against openssl s_server). WSL Ubuntu 26.04 OpenSSL 3.5.5 has SRP but cannot reach a Windows loopback surl (WSL not mirrored). A Linux s_client case is unprobed.
- Surl.Api: no SRP property (AdditionalArguments); `server.TlsSrpAccounts` Add (dup: ArgumentException), SetPassword (unknown user: ArgumentException), Remove (true/false); on a non-SRP server every call InvalidOperationException. surl-mcp `add_tls_srp_account`/`set_tls_srp_account_password`/`remove_tls_srp_account` (server_id, user[, password]) answer `{"user":"..."}`; errors: `user device-2 cannot be added: it holds ':' or already has a TLS-SRP account`, `user nobody has no account`, `server s1 has no TLS-SRP account device`, `server s1 was not started with TLS-SRP`.
- RFC 5054 is not in the bibliography.
- Recipes (batch 12): https-require-a-client-certificate, https-log-in-with-tls-srp (each with -in-csharp and -from-an-agent).

- HTTPS proxy with its own certificate (https-run-an-https-proxy + twins, 2026-11-22, windows): a separate self-signed proxy.pem works with `--proxy-cacert proxy.pem` and no `--ssl-no-revoke`; `--cacert` and `-k` never apply to the proxy, `--proxy-insecure` does. Details in proxy.md (batch 13).

## One test CA, HTTPS and SMTPS (https-trust-a-test-ca-end-to-end + twins; Surl v0.1.8 9f8005dff / Curl 2145a49eae, 2026-11-25, windows)
- A self-signed CA (CA:TRUE, keyCertSign) signing two leaves directly (no intermediate; SAN IP:127.0.0.1, extendedKeyUsage=serverAuth): `--cacert ca.pem` alone is exit 60 `schannel: the revocation status is unknown` on Windows for https, smtps and imaps alike; `--ssl-no-revoke` fixes it. So any CA-signed leaf needs it on Windows, not only chains. Linux/macOS not probed.
- No `--cacert`: 60 `SEC_E_UNTRUSTED_ROOT`. A leaf from another CA with `--cacert ca.pem`: 60; `-k` alone: 0. `openssl verify -CAfile ca.pem a.pem b.pem` prints `a.pem: OK` per file; a stranger exits 2 with empty stdout.
- One surl serves one `--cert`: two certificates means two surls (the mail one needs no `--directory`, so no lock clash with the https one).
- Curl.Api `CertificateAuthorityFile = "ca.pem"` (relative to the working directory) with `SslOptions.NoRevoke` works for https and smtps from one CurlClient; failure `PeerFailedVerification` (60). curl-mcp: use the `curl` tool (`transfer` has no no-revoke field).
