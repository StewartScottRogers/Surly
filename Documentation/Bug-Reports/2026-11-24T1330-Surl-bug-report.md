<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-24 (batch 14 of the 1,000-recipe plan)

From writing and verifying batch 14 (60 recipes) of the *Surly* cookbook against Surl v0.1.5 `d64faa4bb`, Curl `2145a49eae`, Windows 11. Only findings not already filed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | High (security) | `--user-ldap` | A server whose only account source is `--user-ldap` (no `--user`) serves GET and HEAD with no login, 200. Adding one local `--user` makes every request need a login. Configuring a directory should mean logins are required |
| 2 | Medium | Proxy | A forward proxy listening on port 0 does not recognise its own bound port, so a request for that port is forwarded to itself and answered 400. A fixed-port proxy answers 403 `NotAllowed` |
| 3 | Medium | S3 | The AWS SDK's `PutObjectAsync` with its default checksums is answered 501 `NotImplemented`. With `RequestChecksumCalculation = WHEN_REQUIRED` it works |
| 4 | Low | Surl.Api | `server.Files.ListAsync("/team")` throws `No folder at /team` when `ShareAccess` closes the share to clients. The test API should still list it, as it does without `ShareAccess` |

Re-run on 2026-11-24 against the same pins: 1 (it is a passing case in the recipe below) and 2. Items 3 and 4 were seen the same day by the recipe writers on the same pins and not re-run separately.

## Repros

**1.** Recipe `ldap-check-logins-against-a-directory`, server `open`.
- Directory: `surl -v --directory dir --cert server.pem --key server.key -u alice:wonderland -u svc:svcpass ldap://127.0.0.1:P/`
- Web server: `surl --directory open-site --cert server.pem --key server.key --user-ldap "ldap://127.0.0.1:P/ou=people,dc=example,dc=com;bind-dn=cn=svc,dc=example,dc=com;bind-password-file=svc.pass;ca-file=server.pem" https://127.0.0.1:P3/`
- `curl -sS --cacert server.pem -o /dev/null -w "%{http_code}\n" https://127.0.0.1:P3/report.txt` (no `-u`) prints `200`.
- Expected: `401`. With `-u ops:opspass` added to the web server, the same request gets `401`.
- The recipe pins today's behaviour in case `directory-alone-asks-no-login`, so a fix will break that case on purpose. It also works around the problem by adding one local account. Linux and macOS were not probed.

**2.** `surl -v --directory site http://127.0.0.1:0/ --proxy http://127.0.0.1:0/` (`site/hello.txt` present). The log prints two `Listening on` lines; here 59793 (files) and 59794 (proxy).
- `curl -s -o /dev/null -w "%{http_code}" -x http://127.0.0.1:59794/ http://127.0.0.1:59794/hello.txt` prints `400`.
- The log shows `Forwarded GET to 127.0.0.1:59794; the origin answered 400.`
- Expected: `403 NotAllowed`, as a proxy on a fixed port answers for its own address.
- Recipe `http-record-what-a-proxy-passed-on` works around it by using a loopback port with nothing listening on it (502).

**3.** `surl --directory store --cert server.pem --key server.key --user AKIAIOSFODNN7EXAMPLE:s3cret --allow-uploads s3://127.0.0.1:P/`. Then from C#, AWSSDK.S3 4.1.2 with `ServiceURL` pointed at it and default config: `PutObjectAsync(new PutObjectRequest { BucketName = "media", Key = "a.txt", ContentBody = "x" })` throws `AmazonS3Exception` 501 `NotImplemented`.
- Expected: 200.
- With `RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED` the same put gets 200. The SDK's default adds a flexible checksum to the upload (probably an aws-chunked body with a checksum trailer; not confirmed on the wire).
- Recipe `s3-test-an-aws-sdk-client-locally` uses `WHEN_REQUIRED`.

**4.** Surl.Api: an FTP server with a `/team` folder and a `ShareAccess` rule that closes it (recipe `ftp-give-a-group-write-access-in-csharp`; the writer's probe was not kept).
- `await server.Files.ListAsync("/team")` throws `No folder at /team`.
- Expected: the folder's listing, as without `ShareAccess`. The test API is the server owner's view and should not be bound by client share rules.
