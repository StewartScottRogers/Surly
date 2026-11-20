<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-18 20:25 (UTC-7)

These findings come from writing and verifying Part 3 (Secure Servers) of the *Surly* cookbook (`Z:\repos\Surly\Book`). They come on top of `2026-11-18T1734-Surl-bug-report.md`.

**Build tested:** the same as that report: Surl at commit `a0236e175`, `surl 1.0.0 (win-x64)`, with `surl-mcp` and `Surl.Api` built from it. Windows 11, .NET SDK 11.0.100, client the Curl port at `f4b30d981f`. Findings 1 and 2 were reproduced again just before this report was written. Findings 3 and 4 are proved by passing recipe cases.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Low | Options | A token given twice is reported as `user  is given twice`, with an empty name |
| 2 | Low | Docs | `--aihelp ldap` says NTLM binds are accepted with `--auth negotiate`; the build accepts them only with `--auth ntlm` |
| 3 | Question | `surl-mcp` | `remove_account` on the last account opens a login-only server to everyone, silently |
| 4 | Low | `Surl.Api` | `SelfSigned` gives no way to read the certificate it made, so a client can only skip verification |

---

### 1. Error message for a repeated token
**Repro:** `surl --user :a --user :b http://127.0.0.1:0/`
**Observed:** `surl: option --user: user  is given twice`, exit 2. The two spaces are where the empty user name goes.
**Expected:** exit 2 is right, since only one Bearer token is allowed. The message should name the real problem, for example `surl: option --user: a Bearer token (empty user name) is given twice`.

### 2. `--aihelp ldap` and NTLM binds under `--auth negotiate`
`surl --aihelp ldap` says: "The Windows curl's other binds, from its NTLM, Negotiate and Digest options, are NTLM (accepted with `--auth ntlm` or `--auth negotiate`) ...". Against this build, a `curl --ntlm` bind is accepted under `--auth ntlm` but refused under `--auth negotiate`. Under `--auth negotiate`, only `curl --negotiate` binds. The recipe `ldap-bind-with-ntlm` has passing cases for both. Either the help should say "`--ntlm` binds need `--auth ntlm`; `--negotiate` binds carrying NTLM need `--auth negotiate`", or the server should accept a raw NTLM bind under `negotiate` as the help says.

### 3. Removing the last account opens the server (question)
On a server started with one account, `remove_account` on that account succeeds. From then on, a request with no login gets `200`: the server is open to anyone. `add_account` on an open server closes it again at once. This matches the console rule (no `--user` means no login needed), so it may be intended. But an agent tidying up after a test can turn a login-only stand-in into an open one without any sign. Consider either a warning in `remove_account`'s answer when it removes the last account ("server s1 now needs no login"), or refusing to remove the last account without an explicit flag. Recipe: `http-add-an-account-from-an-agent`.

### 4. `Surl.Api`: the `SelfSigned` certificate cannot be read
With `SurlServerOptions.SelfSigned = true`, the server makes a throwaway certificate, but the API exposes neither the certificate nor its public part. A test client therefore cannot trust it and has to turn verification off, the `curl -k` way. A test that wants verification on has to make its own certificate and pass `CertificateFile`/`KeyFile`. The recipe `https-serve-self-signed-in-process` does that with .NET's `CertificateRequest`. Exposing the made certificate, for example `SurlServer.SelfSignedCertificate` as PEM or an `X509Certificate2`, would let a test pin it.
