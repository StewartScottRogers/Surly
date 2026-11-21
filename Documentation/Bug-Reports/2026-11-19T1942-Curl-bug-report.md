<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-19 (batch 1 of the 1,000-recipe plan)

These findings come from writing and verifying batch 1 (60 recipes) of the *Surly* cookbook (`Z:\repos\Surly\Book`), and from three planners surveying what the Curl port can do for the next 890 recipes. They come from the writers' recorded cases, against Surl `763754785` as the server.

**Build tested:** the Curl port at commit `b9ca725077` (branch `work/dark-factory`): `curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`. Windows 11.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | LDAPS (Windows) | `curl --cacert server.pem ldaps://...` still exits 60 (`SEC_E_UNTRUSTED_ROOT`); `Curl.Api`'s `CertificateAuthorityBlob` fails the same way. Only `-k` works |
| 2 | Medium | Curl.Api SMTP | A `byte[]` body is sent as POSTFIELDS, not an upload: `HttpMethod.Put` sends a literal `PUT` (500, exit 8), `Post` sends `VRFY`. A stream body works |
| 3 | Low | `curl-mcp` | `transfer` has no field for the TLS version or `--cacert`; agents must fall back to the `curl` tool |
| 4 | Low | `curl-mcp` | `--output-dir downloads -o ../guide.txt` is refused as `PathRefused`: `-o` is checked against the root and `--output-dir` ignored. Real curl writes inside the root |
| 5 | Ask | Auth | No way to choose Digest SHA-256, or to log in with SCRAM for mail or Kafka |
| 6 | Question | Curl.Api | Does it support Unix sockets and the TFTP block size? Two planned recipes depend on it |

---

### 1. LDAPS ignores `--cacert` on Windows
`surl --cert server.pem --key server.key ... ldaps://127.0.0.1:PORT/`, then `curl --cacert server.pem "ldaps://127.0.0.1:PORT/dc=example,dc=com?cn?sub"`: exit 60, `SEC_E_UNTRUSTED_ROOT`. The same `--cacert` works for `https://` against the same certificate. **Expected:** the LDAP path trusts the file as HTTPS does (the WinLDAP backend may need the certificate passed through its own callback). Linux and macOS not yet checked; the cookbook marks these cases `platforms: [windows]`.

### 2. SMTP from Curl.Api needs a stream body
`new CurlRequest(HttpMethod.Put, smtpUrl + "example.com")` with `Mail = new CurlMailOptions { ... }` and a byte-array body sends `PUT` as an SMTP command (500, `CurlException` 8); with `Post` it sends `VRFY` (252). `Body = CurlContent.FromStream(new MemoryStream(bytes))` delivers the message (250). **Expected:** a byte body to a mail URL uploads, or the API refuses it with a clear message.

### 3 to 6
As in the table.
