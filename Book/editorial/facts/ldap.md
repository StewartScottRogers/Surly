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
- Wrong password: `invalidCredentials`; exit 38 on Windows, exit 67 `Login denied` on Linux and macOS.
- `-u :` (anonymous bind): `inappropriateAuthentication`; exit 38. No `-u`: exit 38 everywhere; Windows
  binds Negotiate as the Windows user (`Authentication Method Not Supported`), Linux and macOS bind anonymously.
- Only WinLDAP names the server's result; Linux and macOS say `LDAP: cannot bind`. See `platforms.md`.

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
- Every refused simple bind other than a wrong password reads `LDAP: cannot bind` on Linux and macOS (exit 38),
  with or without `--auth` (CI run 44662810792).
- `curl --ntlm` and `--negotiate` binds succeed only on Windows (WinLDAP). On Linux and macOS the
  Curl port exits 67 `Login denied`: mark those cases `platforms: [windows]`.
- `curl --ntlm` against `--auth negotiate` is refused `Authentication Method Not Supported`, exit 38, on Windows;
  on Linux and macOS `--ntlm` is exit 67 `Login denied` against any server (the OpenLDAP build has SASL DIGEST-MD5 only).

## LDAPS
- `ldaps://` needs `--cert`/`--key` (or `--self-signed`); without, surl exits 58. A simple bind is checked over `ldaps://` with nothing loosened; the same account on `ldap://` of the same surl is `Confidentiality Required`. One surl can hold both listeners on one directory.
- Windows: curl's ldaps check uses the Windows certificate store, so `--cacert server.pem` does NOT make a self-signed certificate pass (exit 60, schannel SEC_E_UNTRUSTED_ROOT); `-k` does. Same in Curl.Api: `CertificateAuthorityBlob` fails with 60 PeerFailedVerification; `VerifyPeer = false` plus `VerifyHost = false` works. Linux and macOS behaviour (OpenLDAP) not run: those cases are `platforms: [windows]`.

## Surl.Api / Curl.Api / MCP
- Seeding: Surl.Api `await server.Directory.AddEntriesAsync(ldif)` (LDIF text, all or none); surl-mcp `add_ldap_entries(server_id, ldif)` answers `{"entry_count":N}`.
- Client: a GET of `ldap://127.0.0.1:port/<base>?<attrs>?<scope>?<filter>` with `Authentication = new CurlAuthenticationOptions { UserPassword = ":" }` is the anonymous bind (no member says anonymous). Body = curl's text. Failures throw `CurlException`: missing base 39 `LdapSearchFailed`, bind refused 38 `LdapCannotBind` (on Linux and macOS a wrong password is 67 `LoginDenied` and the message is only `LDAP: cannot bind`; on Windows the message holds `Confidentiality Required` / `Invalid Credentials` / `Inappropriate Authentication`, wording otherwise the platform's, `LDAP local: bind via ldap_win_bind ...` on Windows).
- `server.BoundUrls` entries are `Uri`; `.ToString()` before concatenating a search.
- curl-mcp `transfer` has no LDAP or login options: use the `curl` tool (`args`), answer JSON with `standardOutput.content`, `standardError.content`, `exitCode`.

## Recipes
ldap-search-a-directory, ldap-bind-with-ntlm, ldap-search-with-a-filter-and-scope (and its twins), ldaps-search-over-tls (and its twins), ldap-seed-a-directory-in-process, ldap-seed-a-directory-from-an-agent.
ldap-provision-a-user-and-bind-as-it (and its twins), ldap-provision-users-for-a-test.

## Seeding in steps (ldap-seed-a-directory-in-process / -from-an-agent; Surl 9ad60d80c, 2026-11-20, windows)
- A new server's directory is empty: a search for any base is exit 39 (`LdapSearchFailed`). Seeding needs the parent first: a batch starting with `dc=example,dc=com` (domain) and `ou=people` works; entries accumulate across calls on a running server; a second server is empty again.
- A batch is all or none: one entry with a missing parent, a name that exists, or text that is not LDIF is `SurlSeedRefusedException` (Surl.Api) or an `isError` tool result (surl-mcp `add_ldap_entries`; also when the server has no ldap listener); nothing of the batch is added. surl-mcp answers `{"entry_count":N}`.
- `ExportLdifAsync()` / surl-mcp `read_directory(server_id)` (text content, LDIF) hold the seeded entries and omit every `userPassword` value (the attribute name too).
- Searching normalised: split the body on `DN: `, trim lines, sort; works on WinLDAP and OpenLDAP layouts. `-u :` needs `--allow-anonymous`. surl-mcp `curl` with exit 39 has `standardOutput` null, so read `.ValueKind`.
- No `rfc2849` entry in the bibliography.

## Provisioning and binding as the new user (ldap-provision-a-user-and-bind-as-it + twins, ldap-provision-users-for-a-test; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- A simple bind is checked against accounts only (`--user`, `--user-file`, added ones), never an entry's `userPassword`: an entry added alone is found by search but its DN and password bind is refused (Windows 38 `Invalid Credentials`, Linux/macOS 67). Provisioning = add the entry AND an account named the DN's leading `uid`/`cn` value.
- Console seeding at run time, on top of `--directory`: control `POST /v1/directory` (`Content-Type: text/plain`, LDIF body) -> 204; `GET /v1/directory` -> 200 LDIF (`version: 1` first, `userPassword` left out). Events: `store_changed` `{"store":"directory","change":"seeded","name":"<dn>","byte_count":N}`; `login_judged` scheme `ldap`, mechanism `password`, account the bind name (`bob` for `uid=bob,...`).
- `GET /v1/events/wait?kind=login_judged&account=bob&timeout_ms=10000` answers `{"event":{...}}` (the first matching kept event, at once if already there). Surl.Api `WaitForEventAsync(new SurlEventQuery { Kind = StoreChanged, Store = StoreKind.Directory, Name = dn }, timeout)` -> `StoreChangedEvent` (`Store` Directory, `Change` Seeded); surl-mcp `wait_for_event(kinds:["store_changed"], store:"directory", name:dn)`.
- On Windows one refused bind gives TWO `login_judged` refused events in the one exchange (WinLDAP retries); do not count refusals.
- surl-mcp `read_directory`'s text block is JSON `{"ldif":"..."}`; read `StructuredContent.ldif`. `add_account` adds a mail INBOX too (`store_changed` mail `bob/INBOX`).
- `reset_server` (and control `POST /v1/reset`) empties a directory seeded at run time on a server started without one: the search then exits 39. `-u :` without `--allow-anonymous` exits 38 (Windows; Linux/macOS per the table above).

## surl as a directory client: --user-ldap (ldap-check-logins-against-a-directory + twins; Surl v0.1.5 d64faa4bb / Curl 2145a49eae, 2026-11-24, windows)
- `surl ... --user-ldap "ldap://127.0.0.1:P/ou=people,dc=example,dc=com;bind-dn=cn=svc,dc=example,dc=com;bind-password-file=svc.pass;ca-file=server.pem" https://...`: a login no `--user` holds is searched (`uid` = name, scope sub) as svc, then bound as the entry. The directory surl needs `--cert/--key` (StartTLS) and accounts for svc and each user (binds check accounts, not userPassword). curl `-u alice:pw` Basic over https: 200; wrong password, unknown name, a name outside the base: 401. No Surl.Api property: `AdditionalArguments`. surl-mcp: relative `bind-password-file` and `ca-file` resolve under SURL_MCP_ROOT.
- Web `-v` log: `Account source: LDAP directory ldap://127.0.0.1:P` at start; `Login refused: password alice (directory: wrong password)` / `(directory: no such entry)` (no `#N` prefix). Directory `-v` log per login: `LDAP StartTLS accepted`, `Login accepted: simple svc`, `LDAP search <base> scope sub: 1 entries, success`, `Login accepted: simple alice`, then `Exchange N ended because the protocol server threw IOException` (surl's client drops the connection without an unbind).
- LoginJudged events: web one (`https`, alice); directory two (`ldap`, svc then alice).
- `;plaintext` without `--allow-plaintext-auth`: exit 2 `surl: (2) --user-ldap plaintext needs --allow-plaintext-auth` (after the cert check: without `--cert` it is 58 first).
- Finding for Surl (batch-14): a server with `--user-ldap` and no `--user` serves GET/HEAD with no login (200), as with no accounts at all (ADR-0180); one local account makes every request need a login. Recipes add `-u ops:opspass`. Linux/macOS (surl's LDAP client there) not probed; CI checks.

## Managing people in the directory at run time (http-check-logins-against-ldap + twins; Surl v0.1.8 9f8005dff, 2026-11-25, windows)
- With `--user-ldap` on the web server, a person added to the running directory logs in at once without touching the web server, but only with an entry AND a directory account (entry alone: 401, `directory: wrong password`; neither: `directory: no such entry`). Console: directory `--control http://127.0.0.1:P/ --control-token-file f`, `POST /v1/directory` (text/plain LDIF) 204 and `POST /v1/accounts` (`--json`) 204; `DELETE /v1/accounts/alice` 204 -> her next web login 401. Surl.Api: `Directory.AddEntriesAsync` + `Accounts.Add/Remove`; surl-mcp `add_ldap_entries` + `add_account` / `remove_account`.
- A local `-u bob:localpass` on the web server shadows the directory's bob: directory password 401, local 200 (the directory is never asked). Web LoginJudged for a directory login: `https`, `Basic`.
