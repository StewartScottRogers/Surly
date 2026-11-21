<!-- Copyright (c) 2026 Stewart Rogers. -->
# Surl bug report, 2026-11-19 (batch 1 of the 1,000-recipe plan)

These findings come from writing and verifying batch 1 (60 recipes: HTTP, TLS, SFTP, LDAP, mail, test fixtures; console, Surl.Api and `surl-mcp`) of the *Surly* cookbook (`Z:\repos\Surly\Book`), and from three planners surveying what Surl can do for the next 890 recipes. Items 1 to 4 were re-run before writing; the others are from the writers' recorded cases.

**Build tested:** Surl at commit `763754785` (branch `factory/phase-1`): `surl 1.0.0`, `surl-mcp` and `Surl.Api 1.0.0` built from it. Windows 11. Client: the Curl port at `b9ca725077`.

| # | Severity | Area | Summary |
|---|---|---|---|
| 1 | Medium | Logging | `-s` (and `--log-level none`) also hides the `Listening on` line, so a script cannot learn the port surl chose |
| 2 | Medium | Config files | In a `-K` file, a comment after a bare flag (`allow-uploads  # note`) exits 2; after a value it is ignored |
| 3 | Medium | Surl.Api | `ConfigFiles` with empty `ListenUrls` throws `ArgumentException`, though the file has `url` lines and `surl -K` starts |
| 4 | Medium | Surl.Api | `AuthenticationMethods` is a null `IList<string>`, so a collection initializer throws `NullReferenceException` |
| 5 | Medium | Control | `surl --aihelp control` says `--control` is not served yet; the Surl.Api README shows `ControlUrl` answering |
| 6 | Low | HTTP auth | `--auth basic` over `http://`, no login sent: 403 with no `WWW-Authenticate`; a client cannot tell it should switch to HTTPS |
| 7 | Low | HTTP | Every error reply has an empty body, so `curl --fail-with-body` has nothing to keep |
| 8 | Low | TLS | A wrong PFX passphrase is reported as `'server.pfx' is not a P12 certificate Surl can read`; the passphrase is not named |
| 9 | Low | Surl.Api | `StopAsync` takes about 7 s while a client still holds a connection, a few ms once clients are disposed |
| 10 | Low | Surl.Api | No property for the TLS version range (`--tlsv1.3`, `--tls-max`) or `--cert-type`; both need `AdditionalArguments` |
| 11 | Low | Docs | `--aihelp auth` says nothing uses `--group`; `--aihelp messages` accepts `--topic-access @group`. One is wrong |
| 12 | Ask | HTTP | No `If-Modified-Since`, no ETags (only `If-Range`), and no index page (a directory is always 404) |

---

### 1. `-s` hides the listening line
`surl -s --directory site http://127.0.0.1:0/` prints nothing at all, so the bound port of a `:0` listener cannot be read. **Expected:** `-s` drops the exchange log, not the `Listening on` line a caller needs. The cookbook's "find the port it chose" recipe avoids `-s` and says why.

### 2. Comment after a bare flag in a config file
A `-K` file with the line `allow-uploads  # note` stops surl with `fx.conf:3: text follows the value of --allow-uploads; quote a value that holds a space`, exit 2. `directory site  # note` is accepted. **Expected:** the same comment rule for flags and values.

### 3. `ConfigFiles` alone
`new SurlServerOptions { ConfigFiles = { "fixture.conf" } }` where the file holds `url http://127.0.0.1:0/` throws `ArgumentException` (no listen URL) at `StartAsync`. `surl -K fixture.conf` starts. **Expected:** the API reads the file's URLs as the command line does.

### 4. `AuthenticationMethods` is null
`new SurlServerOptions { AuthenticationMethods = { "basic" } }` throws `NullReferenceException`; `AuthenticationMethods = new List<string> { "basic" }` works. Other list properties are initialised.

### 5. The control endpoint
`surl --aihelp control` says `--control` is parsed but not served. The Surl.Api README shows `ControlUrl` answering. Recipes that change accounts or faults while the server runs wait for one of the two to change.

### 6 to 12
Each is described in the table. No. 6: `curl -i http://127.0.0.1:PORT/hello.txt` against `--auth basic --user a:b` gives `HTTP/1.1 403 Forbidden`, `Content-Length: 0`, no `WWW-Authenticate`; the cookbook states this as surl's behaviour. No. 7: `curl -i .../nope.txt` gives `404`, `Content-Length: 0`.
