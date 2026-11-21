<!-- Copyright (c) 2026 Stewart Rogers. -->
# Curl bug report, 2026-11-21 (batch 8 of the 1,000-recipe plan)

From writing and verifying batch 8 (61 recipes) of the *Surly* cookbook against Curl `7d167fa10a`, Surl `9ad60d80c`, Windows 11. Only findings not already filed are listed.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Curl.Api SFTP | `AuthenticationTypes = SshAuthenticationTypes.Password` still tries keyboard-interactive after the password is refused |
| 2 | Low | `curl-mcp` `transfer` | No proxy-login, proxy-CA or `--resolve` field; a body over 1 MiB is truncated (`isTruncated`), with no way to raise the limit |
| 3 | Check | `--retry-max-time` | The retry loop can run past the budget by one wait plus one try; please compare with upstream |

## Repros

**1.** `surl -v --directory site --throwaway-hostkey --user alice:s3cret sftp://127.0.0.1:2222/`; Curl.Api `CurlRequest` to `sftp://127.0.0.1:2222/` with `new CurlSshOptions { AuthenticationTypes = SshAuthenticationTypes.Password }` and a wrong password (the recipe `sftp-refuse-the-first-login-in-csharp` in the cookbook shows the setup). Observed (surl's `-v` log): a password attempt, then a keyboard-interactive attempt. Expected: one password attempt, as libcurl with `CURLSSH_AUTH_PASSWORD` makes.

**2.** curl-mcp `transfer` with `url` to a 3 MiB file. Observed: `isTruncated: true` at 1 MiB. Expected: a field to raise the limit, or the limit in the tool's description. Proxy login and CA, and `--resolve`, need the `curl` tool today.

**3.** `curl -sS -f --retry 3 --retry-delay 3 --retry-max-time 1 http://127.0.0.1:8080/busy` against `--fault "error;path=/busy;reply=503"`. Observed: one retry after 3 s. Expected (upstream reading): no retry once 1 s has passed; please measure against 8.22.0.
