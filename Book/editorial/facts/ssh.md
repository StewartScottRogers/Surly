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
- Surl.Api has no `AuthorizedKeys` property: `AdditionalArguments.Add` each word; `HostKeyFiles.Add("host_key")`. Curl.Api: user in the URL (`sftp://deploy-a@127.0.0.1:port/hello.txt`), `Ssh = new CurlSshOptions { PrivateKeyFile, PublicKeyFile, AuthenticationTypes = SshAuthenticationTypes.PublicKey }`, `Tls = ...VerifyPeer = false` for `-k`; refusal throws `CurlException` `LoginDenied` (67).
- surl-mcp `start_server arguments` take `--hostkey host_key --authorized-keys user:file` with files under `SURL_MCP_ROOT`; curl-mcp `curl` tool `-s -S -k -u deploy-a: --key deploy_a --pubkey deploy_a.pub sftp://...` (exitCode 67, `standardError` `curl: (67) Login denied`, tool error). `ssh-keygen -q` keeps setup quiet. A YAML `run:` with `-u deploy-a: ` needs single quotes.
