<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# http-auth facts (Digest, NTLM, Negotiate, Bearer, Basic)
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19, windows; Linux and macOS from CI output of the same builds.

## Listen URLs and readiness
`http` 80, `https` 443 (needs `--cert` or `--self-signed`); TCP, `ready: port`. One `--directory` per
surl: refusal servers run with none.

## Server options that matter
- `-u` / `--user alice:secret`: once an account exists, every request needs a login.
- `--auth <methods>` (loosens, warns naming the methods): sets what is accepted and offered. HTTP
  words: negotiate, ntlm, digest, basic, bearer, aws-sigv4. `--auth ntlm` offers `WWW-Authenticate:
  NTLM` alone and turns the defaults off. `--auth negotiate` offers `Negotiate` (NTLM inside, or
  Kerberos with `--keytab`). NTLM and Negotiate are bound to the connection; not over HTTP/2.
- `--allow-plaintext-auth` (loosens, warns): Basic and Bearer over `http://` are checked and offered.

## Secure by default
- Default 401 over `http://`: Digest only, three headers in order `algorithm=MD5`, `SHA-256`,
  `SHA-512-256`, same nonce, `realm="surl", qop="auth"`. No NTLM, no Negotiate.
- Basic or Bearer over `http://`: 403 Forbidden, unchecked, right password included.
- `curl --ntlm` against a default server: 401.
- No account and no `--allow-anonymous`: GET/HEAD served, other methods 401 (ADR-0180).

## Client: curl
- `--digest -u`: answers the first challenge (MD5); no option picks another. `--anyauth` picks Digest.
- `--ntlm -u`: logs in over `http://`. Wrong password: 401; `-f` exit 22, stderr has `401`.
- `--negotiate -u`: with no Kerberos, cannot log in anywhere: 401 stands, `-f` exit 22, no stdout.
  From 8.22.0 Negotiate does not fall back to NTLM (upstream commit a8881e5e1d).
- `-s -o {{null}} -w "%{http_code}"` prints the status alone; `-s -i` or `-D -` for headers.

## MCP
curl-mcp `transfer(url, method, headers, ...)` or `curl(args)`. No surl-mcp tool specific to HTTP logins.

## Gotchas
- `curl -v` Digest `Authorization` layout is the platform's: Windows (SSPI) writes
  `username="alice",realm="surl",...,algorithm=MD5,response="...",qop="auth"`; Linux and macOS write
  `username="alice", realm="surl", ..., qop=auth, response="...", algorithm=MD5`. Check fields with
  lookaheads, `(?=[^\r\n]*\balgorithm=MD5\b)`, not the layout.
- `--negotiate` error wording: Windows `SEC_E_NO_CREDENTIALS` (SSPI); Linux `gss_init_sec_context()
  failed: No credentials were supplied...`; macOS `gss_init_sec_context() failed: Unspecified GSS
  failure`. Wording cases are `platforms: [windows]`.
- On Linux and macOS curl prints `Ignoring duplicate digest auth header.` for the 2nd and 3rd challenge.

## Recipes
http-log-in-with-digest, http-log-in-with-ntlm, http-require-a-login, http-refuse-a-password-in-clear.
