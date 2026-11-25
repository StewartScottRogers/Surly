<!-- Copyright (c) 2026 Stewart Rogers. Prose CC BY-ND 4.0. -->
# ssh facts (sftp, scp)
Checked against: surl 1.0.0 (Surl 763754785), curl 8.22.0 (Curl b9ca725077), 2026-11-19, windows. Linux and macOS not run.

## Listen URLs and readiness
`sftp` and `scp`, default port 22; TCP, `ready: port`. One server answers both schemes on one served root. A listen URL needs a host key:
`--hostkey <file>` or `--throwaway-hostkey` (loosens, warns, prints the hash to pin); without either surl exits 2.
One `--directory` per surl: a refusal server runs with none (an empty in-memory root).

## Server options that matter
- `-u alice:s3cret`: the account (password and keyboard-interactive); without any account and without `--allow-anonymous` every login is refused.
- `--list-directories` (widens): lets `sftp://host/folder/` be listed. Off: the listing is answered as if the folder were not there.
- `--allow-uploads` (widens): needed for `-T` uploads, `rename` and `mkdir` quote commands. It is not `--list-directories` that governs `mkdir`.
- `--authorized-keys user:file`: public-key logins (see `sftp-log-in-with-a-public-key`).

## Secure by default
- Listing without `--list-directories`: curl exit 78, `Could not open directory for reading: No such file or directory`, no output.
- `mkdir`, `rename`, upload without `--allow-uploads`: curl exit 21 (quote) or 9 / 25 (upload), `Permission denied`.
- No login or a wrong login: curl exit 67 `Login denied`.

## Client: curl
- `-k` skips the host key check (works with a throwaway key); or pin with `--hostpubsha256`.
- `-l` with a URL ending in `/` lists names one a line; without `-l` the long listing, `-rw-r--r-- 1 surl surl <size> <date> <name>`, `d` first for a folder.
- `-Q "mkdir /reports"` runs before the transfer, so a listing in the same call shows it; `-Q "-mkdir /archive"` (leading dash) runs after. The folder persists across connections. `mkdir` of an existing folder: exit 21, message `mkdir "/reports" failed: Operation failed` (or `Permission denied` when uploads are off).
- Listings are ordered by name; entries printed one a line with `\n`.

## MCP
surl-mcp `start_server(listen_urls, arguments: [--throwaway-hostkey, --user, alice:s3cret, --list-directories, --allow-uploads], files)`; `list_files(server_id, path)` reads the store back, answering `{"entries":[{"name","kind":"folder"|"file","byte_count"}],"has_more"}`: made folders appear as `folder`. curl-mcp: the `curl` tool with `-k -u ...`; its `transfer` tool has no SFTP options.

## Surl.Api / Curl.Api
Server: `ListenUrls = { "sftp://127.0.0.1:0" }`, `ThrowawayHostKey = true`, `Accounts`, `ListDirectories`, `AllowUploads`, `Files[...]`. Client: `CurlRequest(HttpMethod.Get, url)` with `Authentication` (`UserName`, `Password`), `Tls = new CurlTlsOptions { Origin = new CurlTlsPeerOptions { VerifyPeer = false } }` for `-k`, and `Ftp = new CurlFtpOptions { Quote = ["mkdir /reports"] }` for `-Q` (SFTP quote commands live in the FTP record). A GET of a URL ending `/` returns the long listing as the body; Curl.Api has no names-only (`-l`) switch. Failures throw `CurlException`: quote error 21 `QuoteError`, listing refused 78 `RemoteFileNotFound`.

## Gotchas
- `mkdir` needs `--allow-uploads`; a recipe with only `--list-directories` can list but not make folders.
- On the agent surface the message for an existing folder is `Operation failed`; do not match more than `mkdir "/x" failed:`.

## Recipes
sftp-list-and-make-folders, sftp-list-and-make-folders-in-csharp, sftp-list-and-make-folders-from-an-agent; sftp-upload-and-rename and sftp-log-in-with-a-public-key use the same listener; sftp-give-each-user-their-key (and twins).

## One key file per user (sftp-give-each-user-their-key, 2026-11-20, windows)
- `--authorized-keys deploy-a:a_keys --authorized-keys deploy-b:deploy_b.pub` (repeatable; `a_keys` made with `cat deploy_a.pub deploy_a2.pub`, one key a line): each user's keys log in as that user (exit 0); a key offered as another user, a second key offered as another user, and any key for a user with no file are all curl exit 67 `Login denied`.
- Log (`-v`): `Login accepted: publickey deploy-a`. A refused key is NOT a publickey line: curl falls back and the log has `Login refused: password deploy-b` and `Login refused: keyboard-interactive deploy-b` (same exchange). Events say the same: `Mechanism` `publickey` accepted; refusals are `password` / `keyboard-interactive`, even with `SshAuthenticationTypes.PublicKey` on the Curl.Api request.
  - Since Curl 0680b97577 (fix: honour CURLOPT_SSH_AUTH_TYPES) Curl.Api with `SshAuthenticationTypes.PublicKey` does NOT fall back: a refused key is still `LoginDenied` 67, and the server records NO `login_judged` event for it (only exchange_started/ended); checked with Surl a423807a5d, 2026-11-21, windows.
- Surl.Api has no `AuthorizedKeys` property: `AdditionalArguments.Add` each word; `HostKeyFiles.Add("host_key")`. Curl.Api: user in the URL (`sftp://deploy-a@127.0.0.1:port/hello.txt`), `Ssh = new CurlSshOptions { PrivateKeyFile, PublicKeyFile, AuthenticationTypes = SshAuthenticationTypes.PublicKey }`, `Tls = ...VerifyPeer = false` for `-k`; refusal throws `CurlException` `LoginDenied` (67).
- surl-mcp `start_server arguments` take `--hostkey host_key --authorized-keys user:file` with files under `SURL_MCP_ROOT`; curl-mcp `curl` tool `-s -S -k -u deploy-a: --key deploy_a --pubkey deploy_a.pub sftp://...` (exitCode 67, `standardError` `curl: (67) Login denied`, tool error). `ssh-keygen -q` keeps setup quiet. A YAML `run:` with `-u deploy-a: ` needs single quotes.

## Algorithm options and a pinned batch (sftp-offer-a-single-cipher, sftp-upload-a-batch-and-verify-it; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-20, windows)
- `--ssh-ciphers a,b` / `--ssh-macs x` (comma list) narrow what the server offers; the client then agrees on the first of ITS list the server offers. OpenSSH `sftp -v ... 2>&1` prints `kex: server->client cipher: aes256-ctr MAC: hmac-sha2-256` (both directions); default server + defaults: `chacha20-poly1305@openssh.com MAC: <implicit>`; default server + `-o Ciphers=aes256-ctr`: MAC `hmac-sha2-256-etm@openssh.com` (the first default MAC). Wrong client cipher/MAC: exit 255, `Unable to negotiate with 127.0.0.1 port N: no matching cipher found. Their offer: aes256-ctr` (on stdout with `2>&1`; `MAC` likewise). Unknown name: exit 2 `surl: (2) --ssh-ciphers: surl does not offer the SSH cipher bogus` (`--ssh-macs: ... SSH MAC bogus`); weak name: `aes128-cbc needs --allow-weak-ssh-algorithms`. Surl.Api has no property: `AdditionalArguments`; `SurlServerStartException` FailedInit, `Lines[0]` keeps `(2) `.
- The server's KEXINIT is the first packet after `SSH-2.0-surl\r\n`: `server.Exchanges.First().Sent` (C#) or `get_exchanges` chunks `direction sent` (surl-mcp, `data` joined; plaintext). Skip the line, 4 length + 1 padding + 1 type (20) + 16 cookie, then six name lists (4-byte length + comma string): kex, hostkey, ciphers c2s, ciphers s2c, MACs c2s, MACs s2c. Default offer: first cipher `chacha20-poly1305@openssh.com`, includes `aes256-ctr`, no `-cbc`; first MAC `hmac-sha2-256-etm@openssh.com`. Curl.Api/curl cannot narrow their own list.
- curl's SSH library (WinCNG) reads RSA client keys, not ed25519 (`ssh-keygen -t rsa -b 3072`); first curl process costs ~4 s on a loaded machine, so a script uploading a batch uses ONE curl: `-T "dir/{a,b,c}" sftp://h:p/inbox/` (URL ending `/` keeps names), `--ftp-create-dirs`.
- Pin: `--hostpubsha256 <hash>` = `ssh-keygen -l -f host_key.pub | cut -d' ' -f2 | sed 's/^SHA256://'` (base64, no padding, accepted). Wrong pin: curl exit 60 `Denied establishing ssh session: mismatch SHA256 fingerprint`; Curl.Api `CurlSshOptions.HostPublicKeySha256` same hash, `PeerFailedVerification` 60. Uploads off: exit 9 `Upload failed: Permission denied` (Curl.Api `RemoteAccessDenied` 9). Listing without `--list-directories`: exit 78 (`RemoteFileNotFound`). Long listing `-rw-r--r-- 1 surl surl <size> <date> <name>`: name is the last field, size the fifth; sorted by name. A short re-upload replaces the file.
- Curl.Api upload: `CurlRequest(HttpMethod.Put, url) { Body = bytes, Ftp = new CurlFtpOptions { CreateMissingDirectories = FtpCreateMissingDirectories.Create } }`; `SurlAccount("alice","s3cret")` in `Accounts`; `HostKeyFiles.Add("host_key")`.

## Refusing the first login (sftp-refuse-the-first-login, + twins; Surl 9ad60d80c / Curl 7d167fa10a, 2026-11-21, windows)
- `--fault "refuse-login;times=N"` counts login attempts, not connections. curl (and Curl.Api) tries `password` then `keyboard-interactive` on one connection, so `times=1` is spent by the password and keyboard-interactive is accepted (upload succeeds first try, `-v` log `Login refused: password alice` then `Login accepted: keyboard-interactive alice`). `times=2` fails the whole first connection: curl 67 `Login denied`.
- curl `--retry 1` alone does not retry 67 (`%{num_retries}` 0); `--retry-all-errors` does (`1`, exit 0; `-sS` still prints the first `curl: (67) Login denied`; without -s the notice is `Warning: Problem (retrying all errors). Retrying in 1 second. 1 retry left.`). A wrong password is retried too and stays 67.
- A wrong password is refused by the account check before the rule: events `is_refused_by_fault` false and the rule's `remaining` unchanged (2 after two wrong tries).
- Surl.Api: `SurlFault.RefuseLogin() with { Times = 2 }`; `LoginJudgedEvent` {Mechanism `password`/`keyboard-interactive`, Account, IsAccepted, IsRefusedByFault} needs `RecordExchanges = true` (else `FindEvents` throws InvalidOperationException). Curl.Api SFTP upload `HttpMethod.Put` + Body; refusal `CurlException` `LoginDenied`. surl-mcp `get_events kinds ["login_judged"]` fields `exchange_id, scheme, account, mechanism, is_accepted, is_refused_by_fault`; `list_faults` item is `refuse-login` (times not shown), `remaining`. curl-mcp `curl` 67 is a tool error.
- Finding (Curl): Curl.Api `CurlSshOptions.AuthenticationTypes = SshAuthenticationTypes.Password` (2) still makes a keyboard-interactive attempt after the password is refused; upstream libcurl tries keyboard-interactive only with CURLSSH_AUTH_KEYBOARD. Not claimed in a recipe.

- Decided by Surl (2026-11-22): a refused SFTP key publishes no `login_judged` when curl only asks, unsigned, whether the key is acceptable (RFC 4252 s.7): that query is "refused, unchecked" and only checked logins are published (ADR-0128 d.2, ADR-0051). A signed key that is refused does publish `login_judged`.

## A host key kept across restarts (sftp-keep-a-pinned-host-key-across-restarts, + twins; Surl 5da5b3eaf / Curl c84035f797, 2026-11-22, windows)
- `--hostkey host_key` serves the same key at every start (verify's `restart:` kills and restarts; pin still exit 0). A pin can live in a curl config file: `setup: sh pin.sh host_key.pub > pin.cfg` writing `hostpubsha256 = "<base64>"`, then `curl -K pin.cfg` (keeps `$(...)` out of run lines).
- `-v` logs at start `* Serving SSH host key ssh-rsa, --hostpubsha256 <base64>= --hostpubmd5 <hex>`; `--log-file` APPENDS across restarts, so `grep "Serving SSH host key" main.log` shows one line per start. surl-mcp: with `-v` in `arguments`, start_server `log_lines` include that line. Surl.Api: `LogLevel = LogLevel.Verbose` (namespace `Surl.Output`).
- Without -v the log has only `Exchange N opened: sftp ...` (no login lines). With -v: `SSH client identification`, `SSH negotiated kex ...`, `SSH login request: password for alice`, `Login accepted: password alice`, SFTP lines. A client refused on the host key leaves `SSH negotiated kex` and no `login request`; surl-mcp publishes no `login_judged`.
- `--throwaway-hostkey`: a new key each start (two starts, two hashes), warning `surl: warning: --throwaway-hostkey: serving a throwaway SSH host key (--hostpubsha256 <base64>=); clients must pin it or skip the check (curl -k)` (stderr even when the bind then fails 45; first in log_lines; first line of Surl.Api `Log`). Neither option: exit 2 `surl: (2) sftp://127.0.0.1:P/ needs a host key: give --hostkey <file>, or --throwaway-hostkey for a throwaway one` (Surl.Api `Lines[0]` and surl-mcp `lines[0]` keep the `(2) ` prefix).
- Two surl with `--directory` need different folders (exit 124 `Directory site is in use by another surl process`).
- Recipes (batch 12): sftp-keep-a-pinned-host-key-across-restarts (and twins).

## Why a key login failed (sftp-find-why-a-key-login-failed-from-an-agent, Surl d64faa4bb / Curl 2145a49eae, 2026-11-22, windows)
- surl-mcp, `--authorized-keys deploy-a:deploy_a.pub`; curl-mcp `curl -s -S -k -u deploy-a: --key K --pubkey K.pub sftp://...`. Wrong key (deploy_b as deploy-a): exit 67, events `login_judged` deploy-a `password` refused then `keyboard-interactive` refused, no `publickey`; user with no keys (deploy-c): same two events under `deploy-c`; password only (`-u deploy-a:secret`) to a key-only server: the same two events as the wrong key. Right key: one `publickey` accepted. Each refused login takes about a second, so a refused job takes about 3 s; wait for `exchange_ended` (bound 15 s) before reading. `get_events` `exchange_id` groups a connection's events (one connection per curl call).

- Correction (2026-11-22, same recipe): curl DOES offer an unauthorized key. `curl -v` says `SSH: trying public key file ... publickey authentication denied: Username/PublicKey combination invalid`, and surl's `-v` log has `SSH login request: publickey for deploy-a, key ... SHA-256 ...` with no `Login refused: publickey` line; that unsigned query is not published as `login_judged` (Surl decision above), so surl-mcp shows only password and keyboard-interactive refused. A mismatched pair (`--pubkey deploy_a.pub` authorized, `--key deploy_b`) is signed and refused: log `Login refused: publickey deploy-a` twice-requested, event `publickey` `is_accepted` false before the password and keyboard-interactive ones. A server with `--user deploy-a:secret` and no keys, and a password-only job, both leave only password and keyboard-interactive refused. No Curl finding.

## SFTP equivalents of FTP commands (ftp-plan-a-move-from-recorded-traffic, Surl v0.1.8 9f8005dff / Curl 2145a49eae, 2026-11-25, windows)
- `-T --append` appends over SFTP (exit 0); `-Q "rm /in/x"` deletes; `-Q "rename /a /b"` onto an existing `/b` is exit 21 `rename "/a" to "/b" failed: Operation failed` (FTP RNTO replaces); `-Q "rm /b" -Q "rename /a /b"` works. `curl -I sftp://.../f` exits 0 and prints nothing (no Last-Modified); the long listing (folder URL, no `-l`) gives size and date. `-Q "chmod 644 /f"`: exit 21 `Attempt to set SFTP stats ... Operation not supported by SFTP server` (FTP `SITE CHMOD` is 504).
