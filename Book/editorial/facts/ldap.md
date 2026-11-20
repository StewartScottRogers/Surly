<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# ldap facts
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19, windows; Linux and macOS from CI output of the same builds.

## Listen URLs and readiness
`ldap` 389, `ldaps` 636; TCP, `ready: port`. Two surl cannot share one `--directory`, so a second
server for a refusal case runs with no `--directory` (an empty directory; the root DSE still answers).

## Server options that matter
- `--directory site`: serves `site/.surl/ldap/directory.ldif`, read once at start; read-only (add,
  modify, delete, rename refused `unwillingToPerform`). Seed a running server with MCP `add_ldap_entries`.
- `-u alice:s3cret`: an account. Bind name is `alice` or a DN whose leading `cn=`/`uid=` is `alice`.
- `--allow-plaintext-auth` (loosens, warns): checks a simple bind over `ldap://` instead of refusing it.
- `--auth ntlm` (loosens, warns): accepts WinLDAP's raw NTLM bind (`curl --ntlm`). `--auth negotiate`:
  accepts `curl --negotiate` (GSS-SPNEGO carrying NTLM, or Kerberos with `--keytab` and host `localhost`).
  The two words are not interchangeable. `--auth digest-md5` for DIGEST-MD5. Root DSE
  `supportedSASLMechanisms` lists what `--auth` accepts.
- `--allow-anonymous` (loosens, warns): searches without a bind; curl needs `-u :` (empty name and password), which then works on Windows. Without it `-u :` is `inappropriateAuthentication`, exit 38.

## Secure by default
- Simple bind over `ldap://`: `confidentialityRequired`, unchecked; curl exit 38.
- NTLM bind without `--auth ntlm`: `authMethodNotSupported`; exit 38 (Windows).
- Wrong password: `invalidCredentials`; exit 38 (simple bind, all platforms).
- `-u :` (anonymous bind): `inappropriateAuthentication`; exit 38. No `-u`: `Authentication Method
  Not Supported`, exit 38, on all three platforms (Windows binds Negotiate as the Windows user).

## Client: curl
URL `ldap://host:port/<base>?<attrs>?<scope>?<filter>` (RFC 4516); no base reads the root DSE.
Empty result: exit 0, no output. Missing base: exit 39. `-s -S` leaves the error on stderr.
Output, WinLDAP (Windows): `DN: <dn>\n` then `\t<attr>: <value>\n\n` per attribute. OpenLDAP (Linux,
macOS): the same plus one more `\n` after each entry, and the root DSE line is `DN:` with no space.
A pattern for both: `\ADN: ?<dn>\n(\t...\n\n)+\n?` per entry, anchored with `\A`, `\Z`.

## MCP
surl-mcp: `add_ldap_entries(server_id, ldif)` (refused if no LDAP listener), `read_directory(server_id)`.
curl-mcp: `curl(args)` as the command line.

## Gotchas
- Error wording is the library's: Windows `LDAP remote: No Such Object`, Linux `LDAP remote: search
  failed No such object`. Check exit codes, or `2>&1` with `(?i:no such object)`.
- Under `surl --auth ntlm`, a simple bind's refusal reads `Confidentiality Required` on Windows but
  `LDAP: cannot bind` on Linux and macOS (exit 38 both). Without `--auth`, both say `Confidentiality Required`.
- `curl --ntlm` and `--negotiate` binds succeed only on Windows (WinLDAP). On Linux and macOS the
  Curl port exits 67 `Login denied`: mark those cases `platforms: [windows]`.
- `curl --ntlm` against `--auth negotiate` is refused `Authentication Method Not Supported`, exit 38, everywhere.

## LDAPS
- `ldaps://` needs `--cert`/`--key` (or `--self-signed`); without, surl exits 58. A simple bind is checked over `ldaps://` with nothing loosened; the same account on `ldap://` of the same surl is `Confidentiality Required`. One surl can hold both listeners on one directory.
- Windows: curl's ldaps check uses the Windows certificate store, so `--cacert server.pem` does NOT make a self-signed certificate pass (exit 60, schannel SEC_E_UNTRUSTED_ROOT); `-k` does. Same in Curl.Api: `CertificateAuthorityBlob` fails with 60 PeerFailedVerification; `VerifyPeer = false` plus `VerifyHost = false` works. Linux and macOS behaviour (OpenLDAP) not run: those cases are `platforms: [windows]`.

## Surl.Api / Curl.Api / MCP
- Seeding: Surl.Api `await server.Directory.AddEntriesAsync(ldif)` (LDIF text, all or none); surl-mcp `add_ldap_entries(server_id, ldif)` answers `{"entry_count":N}`.
- Client: a GET of `ldap://127.0.0.1:port/<base>?<attrs>?<scope>?<filter>` with `Authentication = new CurlAuthenticationOptions { UserPassword = ":" }` is the anonymous bind (no member says anonymous). Body = curl's text. Failures throw `CurlException`: missing base 39 `LdapSearchFailed`, bind refused 38 `LdapCannotBind` (message holds `Confidentiality Required` / `Invalid Credentials` / `Inappropriate Authentication`, wording otherwise the platform's, `LDAP local: bind via ldap_win_bind ...` on Windows).
- `server.BoundUrls` entries are `Uri`; `.ToString()` before concatenating a search.
- curl-mcp `transfer` has no LDAP or login options: use the `curl` tool (`args`), answer JSON with `standardOutput.content`, `standardError.content`, `exitCode`.

## Recipes
ldap-search-a-directory, ldap-bind-with-ntlm, ldap-search-with-a-filter-and-scope (and its twins), ldaps-search-over-tls (and its twins).
