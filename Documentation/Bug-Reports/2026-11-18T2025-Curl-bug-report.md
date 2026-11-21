<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-18 20:25 (UTC-7)

These findings come from writing and verifying Part 3 (Secure Servers) of the *Surly* cookbook (`Z:\repos\Surly\Book`). They come on top of `2026-11-18T1734-Curl-bug-report.md`.

**Build tested:** the same as that report: Curl at commit `f4b30d981f`, the binary reporting `curl 8.22.0`, with `curl-mcp` and `Curl.Api 0.2.0` built from it. Windows 11, .NET SDK 11.0.100.

The Curl port passed every Part 3 case it was given, across HTTP Basic, Digest, Bearer and NTLM logins, TLS with `--cacert` and `-k`, FTP `AUTH TLS` and `ftps`, STARTTLS and STLS, `wss`, SCP and SFTP with host-key pinning (`--expand-hostpubsha256` from a `--variable` byte range) and public-key logins, LDAP over WinLDAP, and SMB. The only finding is documentation.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Low | `Curl.Api` README | It never shows how to skip certificate verification (the `curl -k` equivalent), and the TLS options are two levels down |

---

### 1. `Curl.Api` README: no `-k` equivalent shown
A reader testing against a self-signed certificate needs the equivalent of `curl -k`. In `Curl.Api` that is `CurlRequest.Tls.Origin.VerifyPeer = false` and `VerifyHost = false`, two levels down, and the README never shows it or names `Origin`. The README's options section lists `CurlTlsOptions` with `CurlTlsPeerOptions`, but a reader has to read the source to find which property is the peer check and which is the host-name check. Please add a sample next to the other option samples, for example "Trust a test server: skip verification, or trust its CA", covering both `VerifyPeer`/`VerifyHost = false` and `CertificateAuthorityBlob` (the safer way, which the recipe `https-serve-self-signed-in-process` uses).

Earlier findings `Curl.Api.Codes` and `Curl.Api.Options.Authentication` (missing from the README's usings) were seen again while writing Part 3. Their status is unchanged.

**Not a Curl defect, for the record:** with `curl --negotiate` against a server that offers `Negotiate`, and no Kerberos, there's no fallback to NTLM (`SEC_E_NO_CREDENTIALS`, exit 22). Upstream curl 8.22.0 behaves the same way.
