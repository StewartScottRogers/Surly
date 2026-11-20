<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# platforms facts
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19; windows run
locally, linux (ubuntu-latest) and macos (macos-latest) from CI run 44662810792 of the same builds.

Only Windows runs locally; CI runs all three. A case whose claim holds on some platforms only gets
`platforms: [...]` and its claim says which; add a case for the other platforms with what they do,
so the recipe still claims something everywhere. Never loosen an `expect` to cover two behaviours
unless the claim is the part they share.

## LDAP: the client library under curl
The Curl port answers as curl's two reference builds (Curl ADR-0166, `LdapDialect`):
- Windows: curl's `lib/ldap.c` over WinLDAP. Every failed bind is exit 38 with WinLDAP's text for the
  server's result: `Invalid Credentials`, `Confidentiality Required`, `Inappropriate Authentication`,
  `Authentication Method Not Supported` (`LDAP local: bind via ldap_win_bind ...`; Curl.Api message the same).
  `--ntlm` and `--negotiate` binds work (Sicily NTLM; GSS-SPNEGO carrying NTLM). No `-u`: binds Negotiate
  as the Windows user.
- Linux and macOS: curl's `lib/openldap.c`. A failed simple bind never names the server's result:
  `invalidCredentials` is exit 67 `Login denied` (Curl.Api `LoginDenied`); every other refusal is
  exit 38 `LDAP: cannot bind` (`LdapCannotBind`). No `-u`: an anonymous bind (refused 38 by surl).
  SASL only DIGEST-MD5: `--ntlm` or `--negotiate` reads the root DSE and gives up with exit 67
  `Login denied` whatever the server offers. A server that closes: exit 7.
- Output layout: OpenLDAP adds one `\n` after each entry; root DSE line `DN:` with no space.
- ldaps certificate check: Windows uses the system store (`--cacert`/`CertificateAuthorityBlob` do not
  help, exit 60); Linux and macOS honour the blob (Curl.Api trusted search succeeded in CI) and say
  `SSL certificate OpenSSL verify result: self-signed certificate (18)` without it.
So: a claim that reads the server's result from stderr is `platforms: [windows]`, with a
`[linux, macos]` twin case checking the exit code (38 or 67) and `LDAP: cannot bind` / `Login denied`.
A wrong password over a plain listener that refuses unread is 38 on Linux, not 67: that is how a
Linux case shows the password was not checked.

## TLS
- curl (all platforms): the port's own TLS client; TLS 1.2 and 1.3 everywhere. `curl: (35)` wording
  differs by platform (Windows mimics Schannel), so match `curl: (35)` or the exit code only.
- surl server: .NET `SslStream`. Windows (Schannel) and Linux (OpenSSL) serve TLS 1.2 and 1.3.
  **macOS serves TLS 1.2 at most** (Surl ADR-0010): the default range negotiates 1.2, a client that
  insists on 1.3 (`--tlsv1.3`, `SslVersion.Tls13`) gets exit 35, and `surl --tlsv1.3` starts but fails
  every handshake (exit 35). Mark cases that need a TLS 1.3 handshake `platforms: [windows, linux]`
  and add a `[macos]` case showing the 35.
- HTTP/3 and WebTransport use the port's own QUIC TLS 1.3 on both sides and pass on macOS.

## UDP and QUIC to a port where nothing listens
- Windows: the ICMP port unreachable comes back to the socket; curl exits 7 at once (CoAP, WebTransport,
  HTTP/3).
- WebTransport (QUIC) on Linux and macOS: exit 55 (send failure), not 7. Mark the 7 `[windows]`, add a
  `[linux, macos]` case expecting 55.
- CoAP on Linux and macOS: no failure at once; curl keeps retransmitting and outlived verify's 60 s
  timeout. Mark the case `[windows]`; no Linux case is practical. `--retry 5 --retry-all-errors
  --retry-delay 1` still works there (the retransmission reaches surl once it is up).
- Readiness for UDP servers stays `ready: none` everywhere.

## Paths and shells
- `-o NUL` is Windows only in prose; cases use `{{null}}`.

## Recipes
ldap-search-a-directory, ldap-bind-with-ntlm, ldap-search-with-a-filter-and-scope, ldaps-search-over-tls
(and twins), https-refuse-clients-below-tls-1-3 (and twins), coap-get-a-resource, webtransport-open-a-stream.
