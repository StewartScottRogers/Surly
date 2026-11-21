<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-20 (batch 6 of the 1,000-recipe plan)

From writing and verifying batch 6 (62 recipes) of the *Surly* cookbook, and from batch 5's CI on Linux and macOS, against Curl `7d167fa10a` and Surl `9ad60d80c`. Only findings not already filed (BL-5594..5618, 5634, 5635) are listed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | **High** | Curl.Api TLS upgrade | `UseSsl = UseSsl.All` on an `ftp://` URL sends `USER` and `PASS` in clear, with no `AUTH TLS` (surl answers 530). On `imap://` the STARTTLS login is refused 67 where `curl --ssl-reqd` succeeds. Credentials leak instead of the request failing |
| 2 | Medium | Connection reuse | `CurlClient` (and `curl --next`) reuse a logged-in IMAP connection for a request with different credentials, so a wrong token on the second request succeeds. Upstream curl does not reuse a connection whose credentials differ |
| 3 | Medium | Curl.Api CA default (Linux, macOS) | With no `CertificateAuthorityBlob`, an untrusted HTTPS peer throws 77 `SslCacertBadfile` on Linux and macOS, 60 `PeerFailedVerification` on Windows. Console curl gives 60. Curl.Api seems to find no default bundle there |
| 4 | Medium | Curl.Api FTPS | About 1 download in 30 fails with 18 `PartialFile`; console curl had 0 failures in 120 against the same server |
| 5 | Medium | Curl.Api POP3 | A custom request `UIDL 2` hangs until the timeout |
| 6 | Low | Curl.Api IMAP | The message after a refused `LOGIN` ends with a stray 0x02 (`Access denied.` + 0x02) |
| 7 | Low | `curl-mcp` | `-T` with a `{a,b}` or `[1-2]` glob is refused; curl accepts it |
| 8 | Ask | S3 | `s3://` has no DELETE (`-X DELETE` exits 4); deletes need `https://` with `--aws-sigv4` |
